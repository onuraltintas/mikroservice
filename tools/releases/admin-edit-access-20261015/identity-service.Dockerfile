ARG BASE_IMAGE
FROM ${BASE_IMAGE}
ARG SOURCE_REVISION
COPY payload/identity-service/ /app/
LABEL org.opencontainers.image.revision="${SOURCE_REVISION}" org.opencontainers.image.release="admin-edit-access-20261015"