#!/bin/sh
set -eu

app_uid="${APP_UID:-1654}"
app_gid="${APP_UID:-1654}"
runtime_secrets_dir=/run/sosubot-secrets

mkdir -p "$runtime_secrets_dir"
chown "$app_uid:$app_gid" "$runtime_secrets_dir"
chmod 0700 "$runtime_secrets_dir"

if [ -f /run/secrets/db-password ]; then
    install -o "$app_uid" -g "$app_gid" -m 0400 \
        /run/secrets/db-password "$runtime_secrets_dir/db-password"
fi

if [ -f /run/config/scores-observer-appsettings.json ]; then
    install -o "$app_uid" -g "$app_gid" -m 0400 \
        /run/config/scores-observer-appsettings.json /app/appsettings.json
fi

exec setpriv \
    --reuid="$app_uid" \
    --regid="$app_gid" \
    --init-groups \
    "$@"
