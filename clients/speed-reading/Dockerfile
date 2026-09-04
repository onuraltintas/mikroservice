# Serve the standalone Speed Reading SPA as an immutable static image.
FROM node:22.14.0-alpine AS build
WORKDIR /workspace/clients/speed-reading

COPY speed-reading/package.json speed-reading/package-lock.json ./
RUN npm ci --legacy-peer-deps --ignore-scripts

COPY speed-reading/ ./
COPY shared/ /workspace/clients/shared/
RUN npm run build -- --configuration production

FROM nginx:1.27.5-alpine AS runtime
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /workspace/clients/speed-reading/dist/speed-reading-frontend/browser /usr/share/nginx/html

EXPOSE 80
HEALTHCHECK --interval=10s --timeout=5s --retries=6 \
  CMD wget --spider --no-verbose http://127.0.0.1/ || exit 1
