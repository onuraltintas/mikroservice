ARG PREVIOUS_WEB_IMAGE=nginx:1.27.5-alpine
FROM ${PREVIOUS_WEB_IMAGE}
ARG SOURCE_REVISION
# Keep previously published lazy-loaded assets available for existing tabs.
COPY web/browser/ /usr/share/nginx/html/
RUN printf '\n// assessment-tachistoscope: %s\n' "$SOURCE_REVISION" >> /usr/share/nginx/html/ngsw-worker.js
LABEL org.opencontainers.image.revision="$SOURCE_REVISION"
