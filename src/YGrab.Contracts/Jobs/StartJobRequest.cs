// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace YGrab.Contracts.Jobs;

/// <param name="Quality">The resolution cap of a video, which an audio job ignores.</param>
/// <param name="Force">Grabs the link even when an earlier job already covers it.</param>
public sealed record StartJobRequest(
    string Link,
    JobModeCode Mode = JobModeCode.Audio,
    VideoQualityCode Quality = VideoQualityCode.P1080,
    bool Force = false
);

public sealed record StartedJobDto(Guid Id);
