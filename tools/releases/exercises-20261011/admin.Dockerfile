FROM node:22.14.0-alpine
WORKDIR /app
ENV NODE_ENV=production
ENV PORT=4000
COPY admin/ ./dist/admin-panel/
LABEL org.opencontainers.image.revision="6f315ff3"
EXPOSE 4000
USER node
ENTRYPOINT ["node", "dist/admin-panel/server/server.mjs"]
