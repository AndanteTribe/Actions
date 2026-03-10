ARG UNITY_EDITOR_IMAGE
FROM ${UNITY_EDITOR_IMAGE}

# /BlankProject is always the activation project path inside the container,
# mounted from the blank-project-path input on the host.
ENTRYPOINT dbus-uuidgen > /etc/machine-id \
  && mkdir -p /var/lib/dbus \
  && ln -sf /etc/machine-id /var/lib/dbus/machine-id \
  && mkdir -p /BlankProject/Assets \
  && unity-editor \
       -batchmode \
       -nographics \
       -quit \
       -logFile - \
       -serial "$UNITY_SERIAL" \
       -username "$UNITY_EMAIL" \
       -password "$UNITY_PASSWORD" \
       -projectPath /BlankProject \
  && exec tail -f /dev/null
