#!/bin/bash
set -e

# dbus setup is required for Unity Editor to function properly in a containerized environment
dbus-uuidgen > /etc/machine-id
mkdir -p /var/lib/dbus
ln -sf /etc/machine-id /var/lib/dbus/machine-id

mkdir -p /BlankProject/Assets

unity-editor -batchmode -nographics -quit \
  -serial "$UNITY_SERIAL" \
  -username "$UNITY_EMAIL" \
  -password "$UNITY_PASSWORD" \
  -projectPath /BlankProject

# Keep the container alive so subsequent steps can exec into it
exec tail -f /dev/null
