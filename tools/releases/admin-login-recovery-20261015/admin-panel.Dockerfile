ARG BASE_IMAGE
FROM ${BASE_IMAGE}
ARG SOURCE_REVISION
USER root
COPY payload/compat/ /app/dist/admin-panel/browser/
COPY payload/admin/ /app/dist/admin-panel/
RUN chown -R node:node /app/dist/admin-panel && chmod -R u=rwX,go=rX /app/dist/admin-panel
USER node
LABEL org.opencontainers.image.revision="${SOURCE_REVISION}" org.opencontainers.image.release="admin-login-recovery-20261015"
