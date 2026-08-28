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
- backup retention.

The setup installs this file as root-owned mode `0600` at `/etc/toured-deploy.conf`. The deployment command refuses a configuration writable by group or others.

## Configure CLI authentication for imports

The two import endpoints use a dedicated CLI identity. They accept exactly one configured bearer token and resolve it to one existing TourEd user. A browser session or arbitrary identity header cannot authorize these routes.

Generate a 256-bit token on the server and install it together with the existing user's email in a root-only environment file. Keep the generated `TOURED_CLI_TOKEN` shell variable in the current trusted administrator session for the first verification call:

```bash
TOURED_CLI_USER_EMAIL='existing-user@example.com'
TOURED_CLI_TOKEN="$(openssl rand -hex 32)"
printf 'Authentication__Cli__UserEmail=%s\nAuthentication__Cli__Token=%s\n' \
    "$TOURED_CLI_USER_EMAIL" "$TOURED_CLI_TOKEN" \
    | sudo tee /etc/toured-api.env >/dev/null
sudo chown root:root /etc/toured-api.env
sudo chmod 0600 /etc/toured-api.env
```

Do not use a newly invented email: the configured address must already exist in the TourEd database. An unknown user is rejected even when the token is correct.

Load the root-protected file through a systemd drop-in rather than placing either value in the visible unit:

```bash
sudo systemctl edit toured-api.service
```

Add these lines in the editor:

```ini
[Service]
EnvironmentFile=/etc/toured-api.env
```

Then reload and restart the service:

```bash
sudo systemctl daemon-reload
sudo systemctl restart toured-api.service
sudo systemctl is-active toured-api.service
```

Use the token without writing its literal value into shell history. For the Touringen import:

```bash
curl --fail-with-body --request POST \
    --header "Authorization: Bearer ${TOURED_CLI_TOKEN}" \
    https://server.example/toured/api/admin/imports/touringen
```

For a user visit import:

```bash
curl --fail-with-body --request POST \
    --header "Authorization: Bearer ${TOURED_CLI_TOKEN}" \
    --form 'csvImport=@/path/to/visits.csv' \
    https://server.example/toured/api/admin/imports
```

Avoid shell tracing and verbose HTTP output while handling the token. In a later administrator session, load it without echoing it by running `read -rsp 'CLI token: ' TOURED_CLI_TOKEN` and pressing Enter.

To rotate the credential, generate a new token, replace only `Authentication__Cli__Token` in `/etc/toured-api.env`, and restart the service. The old token becomes invalid immediately after restart. Verify both import calls with the new value, then run `unset TOURED_CLI_TOKEN` in every shell that held it.

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
- installs `/etc/toured-deploy.conf` and `/usr/local/sbin/deploy-toured` as root-owned files;
- renders and installs the systemd unit from the repository template;
- grants passwordless sudo access only to the deployment command without arguments;
- restarts and verifies the configured service.

Re-run the setup after changing the server script, service template, or configuration. Passing a new public key replaces the old deployment key.

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
7. checks the sibling `/api/points` endpoint and `index.html` once as API and frontend smoke tests.

The API and frontend smoke-test URLs are derived from `HEALTH_URL`, so a value such as `https://server.example/toured/health` checks `https://server.example/toured/api/points` and `https://server.example/toured/index.html`. During rollback, the script waits for the points API instead of `/health`, which keeps rollback compatible with an older application version that does not yet provide the health endpoint.

If migration, startup, readiness, or smoke checking fails, the previous application and database are restored and the old service is restarted. Successful deployments retain the configured number of backups under `BACKUP_ROOT`.
