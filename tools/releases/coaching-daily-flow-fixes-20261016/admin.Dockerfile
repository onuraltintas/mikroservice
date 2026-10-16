ARG BASE_IMAGE
FROM ${BASE_IMAGE}
ARG SOURCE_REVISION
USER root
RUN rm -rf /app/dist/admin-panel
COPY payload/admin/ /app/dist/admin-panel/
USER node
LABEL org.opencontainers.image.revision="${SOURCE_REVISION}" org.opencontainers.image.release="coaching-daily-flow-fixes-20261016-r2"
