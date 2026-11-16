# SPDX-FileCopyrightText: 2026 Thomas Fazzari
# SPDX-License-Identifier: AGPL-3.0-or-later

"""YouTube Music lookups through ytmusicapi, flattened to tuples for the CSnakes bindings.

A failed call raises one of three exceptions, which the C# adapter maps by class name:

- RateLimitedError: YouTube answered 429.
- TransientError: a network error, a timeout, a 403 or a 5xx answer. A retry can succeed.
- UnavailableError: another 4xx answer. A retry will not change it.

Any other exception that escapes is treated as unavailable.
"""

import functools
import re
from http import HTTPStatus
from typing import Annotated, Any

import requests
from ytmusicapi import YTMusic

REQUEST_TIMEOUT_SECONDS = 30
OMV_VIDEO_TYPE = "MUSIC_VIDEO_TYPE_OMV"
SEARCH_LIMIT = 5
DURATION_TOLERANCE_SECONDS = 5
OFFICIAL_SUFFIX = re.compile(r"\s*[(\[][^)\]]*\bofficial\b[^)\]]*[)\]]", re.IGNORECASE)


class TransientError(Exception):
    """A network error, a timeout, a 403 or a server error."""


class RateLimitedError(Exception):
    """YouTube refuses to serve this client for a while."""


class UnavailableError(Exception):
    """YouTube refuses the request and a retry will not change that."""


def _raise_for_status(response: requests.Response, *_args: object, **_kwargs: object) -> None:
    # ytmusicapi parses the body as JSON before it looks at the status
    status = response.status_code
    match status:
        case _ if status < HTTPStatus.BAD_REQUEST:
            return
        case HTTPStatus.TOO_MANY_REQUESTS:
            error: type[Exception] = RateLimitedError
        case HTTPStatus.FORBIDDEN:
            error = TransientError
        case _ if status >= HTTPStatus.INTERNAL_SERVER_ERROR:
            error = TransientError
        case _:
            error = UnavailableError
    reason = f"HTTP {status} {response.reason}"
    response.close()
    raise error(reason)


def _create_client() -> YTMusic:
    # ytmusicapi sets the timeout only on the sessions that it creates
    session = requests.Session()
    session.request = functools.partial(  # ty: ignore[invalid-assignment]
        session.request, timeout=REQUEST_TIMEOUT_SECONDS
    )
    session.hooks["response"].append(_raise_for_status)
    return YTMusic(requests_session=session)


_client = _create_client()


# The hook errors above are not RequestException subclasses, so the handlers below let them pass
def album_of_playlist(playlist_id: str) -> str | None:
    try:
        return _client.get_album_browse_id(playlist_id)
    except requests.RequestException as error:
        raise TransientError(str(error)) from error


def album_of_track(
    video_id: str,
) -> tuple[
    Annotated[str | None, "@BrowseId"],
    Annotated[str | None, "@AudioVideoId"],
]:
    """Find the album of a track and the audio video of that track.

    A music video has no album of its own, so the album comes from its audio counterpart.
    YouTube Music sends the counterpart only to a client that is logged in, so without it
    the album track is searched by title and main artist.
    """
    try:
        tracks = _client.get_watch_playlist(video_id, limit=1)["tracks"]
        match tracks:
            case [{"album": {"id": str(browse_id)}, "videoId": str(audio_id)}, *_]:
                return browse_id, audio_id
            case [{"counterpart": {"album": {"id": str(browse_id)}, "videoId": str(audio_id)}}, *_]:
                return browse_id, audio_id
            case [
                {"title": str(title), "artists": [{"name": str(artist)}, *_]} as track,
                *_,
            ] if track.get("videoType") == OMV_VIDEO_TYPE:
                return _search_album_track(title, artist, _seconds(track.get("length")))
            case _:
                return None, None
    except requests.RequestException as error:
        raise TransientError(str(error)) from error


def _search_album_track(
    title: str, artist: str, length: int | None
) -> tuple[str | None, str | None]:
    # A music video often adds an intro or an outro, so the album track can be shorter
    wanted = _plain_title(title).casefold()
    longest = None if length is None else length + DURATION_TOLERANCE_SECONDS
    found = _client.search(f"{artist} {_plain_title(title)}", filter="songs", limit=SEARCH_LIMIT)
    for result in found:
        match result:
            case {
                "videoId": str(audio_id),
                "title": str(name),
                "artists": [{"name": str(first)}, *_],
                "album": {"id": str(browse_id)},
            } if (
                _plain_title(name).casefold() == wanted
                and first.casefold() == artist.casefold()
                and (longest is None or result.get("duration_seconds", 0) <= longest)
            ):
                return browse_id, audio_id
            case _:
                continue
    return None, None


def _plain_title(title: str) -> str:
    return OFFICIAL_SUFFIX.sub("", title).strip()


def _seconds(length: object) -> int | None:
    match length:
        case str() if all(part.isdigit() for part in length.split(":")):
            return functools.reduce(
                lambda total, part: total * 60 + int(part), length.split(":"), 0
            )
        case _:
            return None


def album(
    browse_id: str, video_id: str | None = None
) -> tuple[
    Annotated[str, "@Title"],
    Annotated[list[str], "@Artists"],
    Annotated[str | None, "@Year"],
    Annotated[str, "@CoverUrl"],
    Annotated[int, "@TrackCount"],
    Annotated[
        list[
            tuple[
                Annotated[str, "@VideoId"],
                Annotated[str, "@Title"],
                Annotated[list[str], "@Artists"],
                Annotated[int, "@Number"],
                Annotated[int, "@Seconds"],
                Annotated[bool, "@Explicit"],
            ]
        ],
        "@Tracks",
    ],
]:
    """Read an album.

    With video_id, keeps only the track of that audio video.
    """
    try:
        found = _client.get_album(browse_id)
        available = _available_tracks(found)
        audio_ids = _audio_video_ids(found, available)
    except requests.RequestException as error:
        raise TransientError(str(error)) from error
    tracks = []
    for (position, track), audio_id in zip(available, audio_ids, strict=True):
        if video_id is not None and video_id not in (audio_id, track["videoId"]):
            continue
        tracks.append(
            (
                audio_id,
                track["title"],
                _names(track["artists"]),
                track.get("trackNumber") or position,
                track.get("duration_seconds") or 0,
                bool(track.get("isExplicit")),
            )
        )
    return (
        found["title"],
        _names(found["artists"]),
        found.get("year"),
        max(found["thumbnails"], key=lambda thumbnail: thumbnail["width"])["url"],
        len(found["tracks"]),
        tracks,
    )


def _available_tracks(found: dict[str, Any]) -> list[tuple[int, dict[str, Any]]]:
    return [
        (position, track)
        for position, track in enumerate(found["tracks"], start=1)
        if track.get("isAvailable", True) and track.get("videoId")
    ]


def _audio_video_ids(
    found: dict[str, Any], available: list[tuple[int, dict[str, Any]]]
) -> list[str]:
    # Album tracks can point at music videos, and the audio playlist lists the audio tracks
    # Several tracks can point at the same music video, so the match goes by position
    own = [track["videoId"] for _, track in available]
    playlist_id = found.get("audioPlaylistId")
    if not playlist_id or not any(
        track.get("videoType") == OMV_VIDEO_TYPE for _, track in available
    ):
        return own
    items = _client.get_playlist(playlist_id, limit=None)["tracks"]
    if len(items) != len(available):
        return own
    return [item.get("videoId") or video_id for video_id, item in zip(own, items, strict=True)]


def _names(artists: list[dict[str, Any]] | None) -> list[str]:
    return [artist["name"] for artist in artists or []]
