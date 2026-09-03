# Production deployment

TourEd is deployed manually from GitHub Actions. The workflow only accepts runs from `master`; connection details live in the GitHub `production` environment, while privileged server settings live in `/etc/toured-deploy.conf`.

## Determine the server architecture

Run this once on the server:

```bash
uname -m
```

Use `linux-x64` for `x86_64`, or `linux-arm64` for `aarch64`/`arm64`. The workflow builds only this migration-bundle architecture.

## Create a dedicated SSH key

Create the deployment key on a trusted workstation. It deliberately has no passphrase because GitHub Actions cannot answer an interactive prompt.

```bash
ssh-keygen -t ed25519 -a 100 -N '' -f toured-github-actions -C github-actions@toured
```

Keep `toured-github-actions` private. Only its `.pub` file is installed on the server.

## Prepare the server configuration

Copy and adapt the generic example locally:

```bash
cp deploy/server/toured-deploy.conf.example toured-deploy.conf
```

The example deliberately contains no production hostnames, account names, private addresses, or installation paths. Before running the setup, replace its generic values with those of the existing server installation. These commands help identify the current service settings:

```bash
systemctl show toured-api.service --property=User,Group,ExecStart,WorkingDirectory
command -v dotnet
```

The file configures:

- the isolated SSH deployment account and home directory;
- runtime user and group;
- application, database, and backup paths;
- systemd service name, .NET executable, and listen URL;
- the public `HEALTH_URL`, which must end in `/health` and is used to decide success or rollback;
- the root-owned runtime environment file and persistent Data-Protection key directory;
- backup retention.

The setup installs this file as root-owned mode `0600` at `/etc/toured-deploy.conf`. The deployment command refuses a configuration writable by group or others.

## Configure Google OAuth

Create an OAuth 2.0 client of type **Web application** in Google Cloud. Configure the exact public callback URI, including the deployed path base:

```text
https://toured-app.de/signin-google
```

For local development, add only the exact HTTPS callback URI and port actually used, for example `https://localhost:7082/signin-google`. Google redirects to the backend callback; no JavaScript origin is needed by TourEd. While the consent screen remains in testing mode, add every permitted Google account as a test user.

The Google account must have a verified email matching an existing, unbound TourEd user on its first login. TourEd never creates a user from Google. Once bound, the stable Google subject identifies the user and later email changes do not rebind the account.

## Configure the runtime environment

The root-owned runtime environment file contains the Google credentials, CLI identity, public path base, persistent Data-Protection location, reverse-proxy forwarding switch, Touringen import source, and SMTP notification settings. None of the secret values belongs in the repository, release artifact, visible systemd unit, pull request, issue, or chat.

Before deploying this version for the first time, the new notification environment entries must be added to `/etc/toured-api.env` so that deployment validation succeeds.

Generate a 256-bit CLI token on the server and enter the Google client secret and SMTP password interactively without placing literals in shell history or shell arguments (alternatively edit `/etc/toured-api.env` using `sudoedit` and verify `root:root` with mode `0600` afterwards). The CLI email must already exist in the TourEd database. For IONOS SMTP (`smtp.ionos.de:587` with mandatory STARTTLS), the authenticated mailbox account must be permitted to use the configured sender address:

```bash
TOURED_GOOGLE_CLIENT_ID='client-id.apps.googleusercontent.com'
TOURED_CLI_USER_EMAIL='existing-user@example.com'
TOURED_CLI_TOKEN="$(openssl rand -hex 32)"
read -rsp 'Google client secret: ' TOURED_GOOGLE_CLIENT_SECRET
printf '\n'
read -rsp 'SMTP password: ' TOURED_SMTP_PASSWORD
printf '\n'
printf 'Authentication__Google__ClientId=%s\nAuthentication__Google__ClientSecret=%s\nAuthentication__Cli__UserEmail=%s\nAuthentication__Cli__Token=%s\nPathBase=%s\nDataProtection__KeysPath=%s\nASPNETCORE_FORWARDEDHEADERS_ENABLED=%s\ntouringen__StempelstellenUri=%s\nRegistrationNotifications__Enabled=%s\nRegistrationNotifications__SmtpHost=%s\nRegistrationNotifications__SmtpPort=%s\nRegistrationNotifications__SmtpUsername=%s\nRegistrationNotifications__SmtpPassword=%s\nRegistrationNotifications__SenderAddress=%s\nRegistrationNotifications__RecipientAddress=%s\n' \
    "$TOURED_GOOGLE_CLIENT_ID" \
    "$TOURED_GOOGLE_CLIENT_SECRET" \
    "$TOURED_CLI_USER_EMAIL" \
    "$TOURED_CLI_TOKEN" \
    '' \
    '/srv/toured/data-protection-keys' \
    'true' \
    'https://www.touringen.de/stempelstellen' \
    'true' \
    'smtp.ionos.de' \
    '587' \
    'smtp-user@example.com' \
    "$TOURED_SMTP_PASSWORD" \
    'sender@example.com' \
    'admin-recipient@example.com' \
    | sudo tee /etc/toured-api.env >/dev/null
sudo chown root:root /etc/toured-api.env
sudo chmod 0600 /etc/toured-api.env
unset TOURED_GOOGLE_CLIENT_SECRET
unset TOURED_SMTP_PASSWORD
```

Replace all `example.com` addresses with the actual accounts before writing the environment file. `DataProtection__KeysPath` must exactly match `DATA_PROTECTION_KEYS_DIR` in `toured-deploy.conf`. `PathBase` is empty when TourEd is hosted at the domain root; a non-empty path base starts with `/` and has no trailing slash. The checked-in `touringen` settings supply the official Touringen page and GPX archive URLs used by the terminal-driven import; deployments normally override only `touringen__StempelstellenUri` if necessary. No separate test email is sent; the background notification service evaluates pending requests immediately after application startup and then every five minutes.

The reverse proxy sends the original host and HTTPS protocol through forwarded headers. For the current domain-root deployment:

```nginx
location / {
    proxy_pass http://private-backend:5000;
    proxy_set_header Host $host;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header X-Forwarded-Proto $scheme;
}
```

`ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` lets ASP.NET Core accept those proxy headers when the proxy is not on loopback, such as nginx in Docker. Because this mode accepts forwarded headers from any immediate peer, the configured `LISTEN_URL` must expose Kestrel only to the trusted proxy host or private network and never directly to the public internet.

The server setup validates that the environment file is a regular root-owned file inaccessible to group and others, requires every expected setting exactly once, creates the persistent key directory for the runtime user with mode `0700`, and renders the environment-file path into the systemd unit.

## Use CLI authentication for administration and imports

The admin endpoints use a dedicated CLI identity. They accept exactly one configured bearer token and resolve it to one existing TourEd user. A browser session or arbitrary identity header cannot authorize these routes.

The separately maintained local `TourEd.Admin` terminal client reads this token from a configurable file, uses only HTTP(S), and can list users/providers, replace one user's provider entitlements, and start the Touringen or HWN imports. The server API remains usable directly. For example:

```bash
curl --fail-with-body \
    --header "Authorization: Bearer ${TOURED_CLI_TOKEN}" \
    https://toured-app.de/api/admin/users

curl --fail-with-body --request PUT \
    --header "Authorization: Bearer ${TOURED_CLI_TOKEN}" \
    --header "Content-Type: application/json" \
    --data '{"providers":["touringen","harzer-wandernadel"],"defaultProvider":"touringen"}' \
    https://toured-app.de/api/admin/users/42/providers
```

The update replaces the complete entitlement set atomically. Its default provider must be included in that set. Grants, revocations, and default-provider changes are written to the database audit log using internal actor/target ids, action, timestamp, and optional provider slug; tokens and email addresses are not duplicated there. Audit entries are retained for 90 days and cleaned up automatically upon application startup and every 24 hours thereafter, requiring no manual administrative operations. Database backups retain audit entries according to the configured backup rotation; restoring a backup causes the startup cleanup to delete already expired audit entries immediately.

Use the token without writing its literal value into shell history. For the Touringen import:

```bash
curl --fail-with-body --request POST \
    --header "Authorization: Bearer ${TOURED_CLI_TOKEN}" \
    https://toured-app.de/api/admin/imports/touringen
```

The Touringen import reads the 430 standard stamping points directly from OSM relation 14773147, the 8 Naturschätze and 13 Rhön points from the official GPX archives, and hiking tours from the Touringen website. It updates points in place while retaining internal IDs and user visits, records OSM provenance under ODbL 1.0, and makes the public GeoJSON export `GET /api/providers/touringen/points.geojson` available.

For the Harzer Wandernadel import:

```bash
curl --fail-with-body --request POST \
    --header "Authorization: Bearer ${TOURED_CLI_TOKEN}" \
    https://toured-app.de/api/admin/imports/harzer-wandernadel
```

The HWN import reads OSM relation 148007 and requires exactly one usable summer location for every regular number from 1 through 222. The winter alternative for HWN 69 is intentionally excluded. A complete import updates existing points without changing their internal ids or visits, records the OSM relation revision and licence metadata, and enables anonymous HWN access atomically. Until the first successful OSM import, HWN remains restricted and the public GeoJSON endpoint is unavailable. The non-secret relation id, OSM API/public URLs, and download-size limit are configured in the deployed appsettings under `harzerWandernadel`.

After deploying this change, run the HWN import once with the CLI token. Then verify anonymous `GET /api/points?provider=harzer-wandernadel` and `GET /api/providers/harzer-wandernadel/points.geojson`; the latter contains only public provider point data and ODbL provenance, never accounts or visits.

For a user visit import:

```bash
curl --fail-with-body --request POST \
    --header "Authorization: Bearer ${TOURED_CLI_TOKEN}" \
    --form 'csvImport=@/path/to/visits.csv' \
    https://toured-app.de/api/admin/imports
```

For inserting or updating stamping points (e.g. temporary Sonderstempel):

```bash
curl --fail-with-body --request POST \
    --header "Authorization: Bearer ${TOURED_CLI_TOKEN}" \
    --header "Content-Type: application/json" \
    --data '[
      {
        "provider": "touringen",
        "series": "sonderstempel",
        "name": "Landesgartenschau Leinefelde-Worbis",
        "latitude": 51.385012,
        "longitude": 10.325123,
        "externalId": "sonderstempel-lgs-worbis-2026",
        "validFrom": "2026-04-23",
        "validUntil": "2026-10-11"
      }
    ]' \
    https://toured-app.de/api/admin/points
```

Existing points matched by `(series, number)` or `(provider, externalId)` are updated in place while preserving internal database IDs and all recorded user visits; new points are created.

Avoid shell tracing and verbose HTTP output while handling the token. In a later administrator session, load it without echoing it by running `read -rsp 'CLI token: ' TOURED_CLI_TOKEN` and pressing Enter.

To rotate the credential, generate a new token, replace only `Authentication__Cli__Token` in `/etc/toured-api.env`, and restart the service. The old token becomes invalid immediately after restart. Verify the required import calls with the new value, then run `unset TOURED_CLI_TOKEN` in every shell that held it. Rotate the Google client secret or SMTP password the same way and restart the service after replacing `Authentication__Google__ClientSecret` or `RegistrationNotifications__SmtpPassword`.

## Run the one-time server setup

Copy the server files, configuration, and public key while logged in as the existing administrator:

```bash
tar -czf /tmp/toured-server-setup.tar.gz deploy/server
scp /tmp/toured-server-setup.tar.gz toured-deploy.conf toured-github-actions.pub admin@server.example:/tmp/
ssh admin@server.example
mkdir -p /tmp/toured-server-setup
tar -xzf /tmp/toured-server-setup.tar.gz -C /tmp/toured-server-setup
sudo /tmp/toured-server-setup/deploy/server/setup-toured-deployment \
    /tmp/toured-github-actions.pub \
    /tmp/toured-deploy.conf
```

The setup:

- creates the password-locked, SSH-key-only deployment user;
- manages its single authorized key with forwarding and PTY restrictions;
- creates an isolated upload directory;
- creates the runtime group and applies private application/database permissions;
- validates the root-owned runtime environment file and creates the persistent Data-Protection key directory;
- installs `/etc/toured-deploy.conf` and `/usr/local/sbin/deploy-toured` as root-owned files;
- renders and installs the systemd unit with the configured environment file;
- grants passwordless sudo access only to the deployment command without arguments;
- restarts and verifies the configured service.

Re-run the setup after changing the server script, service template, deployment configuration, runtime-environment path, or Data-Protection path. Passing a new public key replaces the old deployment key. Changing only a credential value requires a service restart, not another setup run.

## Pin the SSH host key

Obtain the trusted Ed25519 host-key fingerprint directly on the server:

```bash
sudo ssh-keygen -lf /etc/ssh/ssh_host_ed25519_key.pub
```

On the trusted workstation, collect the public key and compare its fingerprint with the server output:

```bash
ssh-keyscan -p 22 -t ed25519 server.example > toured-known-hosts
ssh-keygen -lf toured-known-hosts
```

Do not store the result until the fingerprints match.

## Configure the GitHub production environment

Open **Repository settings → Environments**, create or select the environment named `production`, and add these environment variables:

- `TOURED_DEPLOY_HOST`: SSH hostname or IP address
- `TOURED_DEPLOY_PORT`: SSH port, normally `22`
- `TOURED_DEPLOY_USER`, matching `DEPLOY_USER` in the server config
- `TOURED_DEPLOY_HOME`, matching `DEPLOY_HOME` in the server config
- `TOURED_DEPLOY_RUNTIME`, derived from `uname -m` above
- `TOURED_PUBLIC_URL`: public URL of the bundled map

In the same `production` environment, add these environment secrets:

- `TOURED_DEPLOY_SSH_PRIVATE_KEY`: complete contents of `toured-github-actions`
- `TOURED_DEPLOY_KNOWN_HOSTS`: verified contents of `toured-known-hosts`

The private key must never be committed or copied into an issue, pull request, or chat. Optional required reviewers on the `production` environment add a second confirmation after clicking **Run workflow**.

## Run a deployment

1. Open the repository's **Actions** tab.
2. Select **Deploy production**.
3. Choose **Run workflow** on `master`.

The workflow restores, builds, and tests the solution, publishes the API, creates the configured EF Core migration bundle, uploads a checksummed release, and invokes the restricted server command.

The server then:

1. loads and validates its root-owned configuration;
2. validates and extracts the release;
3. stops the configured service;
4. backs up the complete previous application and SQLite database;
5. installs the new application and applies EF Core migrations as the runtime user;
6. starts the service and waits for the configured `/health` readiness check;
7. checks `/auth/session` and `index.html` once as API and frontend smoke tests.

The API and frontend smoke-test URLs are derived from `HEALTH_URL`, so the current value `https://toured-app.de/health` checks `https://toured-app.de/auth/session` and `https://toured-app.de/index.html`. During rollback, the script waits for `/health`.

If migration, startup, readiness, or smoke checking fails, the previous application and database are restored and the old service is restarted. Successful deployments retain the configured number of backups under `BACKUP_ROOT`.
