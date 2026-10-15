ARG BASE_IMAGE
FROM ${BASE_IMAGE}
ARG SOURCE_REVISION
USER root
RUN rm -rf /app/dist/admin-panel
COPY payload/admin/ /app/dist/admin-panel/
RUN chown -R node:node /app/dist/admin-panel && chmod -R u=rwX,go=rX /app/dist/admin-panel
USER node
LABEL org.opencontainers.image.revision="${SOURCE_REVISION}" org.opencontainers.image.release="admin-user-dual-service-20261015"
