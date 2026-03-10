ARG UNITY_EDITOR_IMAGE
FROM ${UNITY_EDITOR_IMAGE}

# Set up dbus machine-id (required by Unity) and keep the container alive
# so subsequent docker exec steps can work with the mounted project.
ENTRYPOINT dbus-uuidgen > /etc/machine-id \
  && mkdir -p /var/lib/dbus \
  && ln -sf /etc/machine-id /var/lib/dbus/machine-id \
  && exec tail -f /dev/null
