FROM nginx:1.27.5-alpine
ARG SOURCE_REVISION
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY previous/browser/ /usr/share/nginx/html/
COPY web/browser/ /usr/share/nginx/html/
RUN printf '\n// speed-session-cleanup-20261013: %s\n' "$SOURCE_REVISION" >> /usr/share/nginx/html/ngsw-worker.js
LABEL org.opencontainers.image.revision="$SOURCE_REVISION"
EXPOSE 80
HEALTHCHECK --interval=10s --timeout=5s --retries=6 CMD wget --spider --no-verbose http://127.0.0.1/ || exit 1
