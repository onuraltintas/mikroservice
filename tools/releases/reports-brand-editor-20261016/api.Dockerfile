ARG BASE_IMAGE
FROM ${BASE_IMAGE}
ARG SOURCE_REVISION
COPY payload/speed-api/ /app/
LABEL org.opencontainers.image.revision="${SOURCE_REVISION}" org.opencontainers.image.release="reports-brand-editor-20261016"
