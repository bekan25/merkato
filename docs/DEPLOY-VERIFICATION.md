# MERKATO deployment verification

This procedure verifies which Git branch and commit the Render service is actually running. A successful GitHub Actions run alone is not deployment evidence.

## 1. Confirm the Blueprint configuration

1. Open the Render Dashboard: https://dashboard.render.com/
2. Select the `merkato-bekan25` web service.
3. Open **Settings** and inspect the connected repository and branch.
4. Confirm the repository is `bekan25/merkato` and the branch is exactly `main`.
5. Confirm the service uses the repository Blueprint configuration `render.yaml`, Docker runtime, and `backend/Dockerfile`.
6. If the tracked branch is not `main`, do not change it without the service owner's approval. Record the observed branch and stop the verification.

## 2. Confirm the deployed commit in Render

1. Open the service's **Events** tab.
2. Find the latest successful deploy event (not merely the latest build attempt).
3. Open the event details and record the deployed commit SHA and deploy time.
4. Open the **Logs** tab and confirm the application started successfully after that deploy.
5. Do not infer the deployed commit from the current GitHub main HEAD; compare the actual Render event SHA.

## 3. Verify the running application's version endpoint

After the service has finished starting (the free instance may need several minutes to wake), request:

```http
GET https://merkato-bekan25.onrender.com/api/version
```

Expected JSON shape:

```json
{
  "version": "1.0.0",
  "commitSha": "<deployed-commit-sha>",
  "buildTimeUtc": "<UTC timestamp>",
  "branch": "main"
}
```

The `commitSha` should match the SHA shown in the latest successful Render deploy event. The `branch` should be `main`. If either value is `unknown`, missing, or does not match Render's event, mark deployed-version verification **UNVERIFIED** and investigate before claiming the intended version is live.

The endpoint prefers Render's runtime-provided `RENDER_GIT_COMMIT` and `RENDER_GIT_BRANCH` values. The Docker image also accepts non-secret `MERKATO_VERSION`, `MERKATO_COMMIT_SHA`, and `MERKATO_BRANCH` build arguments. Locally, unavailable metadata deliberately falls back to `unknown`.

## 4. Verify readiness separately

Request:

```http
GET https://merkato-bekan25.onrender.com/health/ready
```

Expect HTTP 200 and JSON containing `{"status":"ready"}`. A successful readiness response proves database connectivity at that moment; it does not prove checkout, payment, delivery, image upload, or complete production readiness.

## 5. Evidence to record

Record all of the following in the release or deployment report:

- Render service name and service URL.
- Tracked repository and branch from **Settings**.
- Latest successful deploy event URL and deployed commit SHA.
- `/api/version` HTTP status and returned `version`, `commitSha`, `buildTimeUtc`, and `branch`.
- `/health/ready` HTTP status and response.
- UTC verification time.

Never include environment variable values, API keys, database connection strings, JWT keys, or other secrets in evidence.
