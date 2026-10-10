FROM nginx:1.27.5-alpine
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY web/browser/ /usr/share/nginx/html/
RUN printf '\n// release: child-programs-v2-77618cb9\n' >> /usr/share/nginx/html/ngsw-worker.js
LABEL org.opencontainers.image.revision="77618cb9"
EXPOSE 80
HEALTHCHECK --interval=10s --timeout=5s --retries=6 CMD wget --spider --no-verbose http://127.0.0.1/ || exit 1
