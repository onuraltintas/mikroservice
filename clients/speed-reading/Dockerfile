# Serve the standalone Speed Reading SPA as an immutable static image.
FROM node:22.14.0-alpine AS build
WORKDIR /workspace/clients/speed-reading

COPY speed-reading/package.json speed-reading/package-lock.json ./
RUN npm ci --legacy-peer-deps --ignore-scripts

COPY speed-reading/ ./
COPY shared/ /workspace/clients/shared/
RUN npm run build -- --configuration production

# Unprivileged nginx: runs as UID 101 and listens on 8080.
FROM nginxinc/nginx-unprivileged:1.27.5-alpine AS runtime
# Assemble the image as root; the files stay root-owned and world-readable.
USER root
COPY speed-reading/nginx.conf /etc/nginx/conf.d/default.conf
COPY speed-reading/nginx-security-headers.conf /etc/nginx/snippets/security-headers.conf
COPY --from=build /workspace/clients/speed-reading/dist/speed-reading-frontend/browser /usr/share/nginx/html
USER 101

EXPOSE 8080
HEALTHCHECK --interval=10s --timeout=5s --retries=6 \
  CMD wget --spider --no-verbose http://127.0.0.1:8080/ || exit 1
