export const REPOSITORY_URL = "https://github.com/thomas-fazzari/ygrab";

export const commitUrl = (sha: string) => `${REPOSITORY_URL}/commit/${sha}`;

export const profileUrl = (login: string) => `https://github.com/${login}`;
