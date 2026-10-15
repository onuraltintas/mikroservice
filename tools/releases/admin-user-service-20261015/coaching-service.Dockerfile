ARG BASE_IMAGE
FROM ${BASE_IMAGE}
ARG SOURCE_REVISION
COPY payload/coaching-service/ /app/
LABEL org.opencontainers.image.revision="${SOURCE_REVISION}" org.opencontainers.image.release="admin-user-service-20261015"