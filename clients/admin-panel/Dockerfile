# Build the Angular SSR application in an isolated Node image.
FROM node:22.14.0-alpine AS build
WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci --legacy-peer-deps --ignore-scripts

COPY . .
RUN npm run build -- --configuration production

# Keep only the production SSR runtime in the final image.
FROM node:22.14.0-alpine AS runtime
WORKDIR /app
ENV NODE_ENV=production
ENV PORT=4000

COPY --from=build /app/dist/admin-panel ./dist/admin-panel

EXPOSE 4000
USER node
ENTRYPOINT ["node", "dist/admin-panel/server/server.mjs"]
