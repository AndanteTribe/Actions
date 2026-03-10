ARG UNITY_EDITOR_IMAGE
FROM ${UNITY_EDITOR_IMAGE}

COPY entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

ENTRYPOINT ["/entrypoint.sh"]
