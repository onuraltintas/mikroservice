ARG PREVIOUS_WEB_IMAGE
FROM ${PREVIOUS_WEB_IMAGE}
ARG SOURCE_REVISION
# Keep previously published lazy-loaded assets available for existing tabs.
COPY web/browser/ /usr/share/nginx/html/
RUN printf '\n// assessment-tachistoscope: %s\n' "$SOURCE_REVISION" >> /usr/share/nginx/html/ngsw-worker.js
LABEL org.opencontainers.image.revision="$SOURCE_REVISION"
