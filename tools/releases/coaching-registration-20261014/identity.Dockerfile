ARG BASE_IMAGE
FROM ${BASE_IMAGE}
ARG SOURCE_REVISION
COPY payload/identity/ /app/
LABEL org.opencontainers.image.revision="${SOURCE_REVISION}" org.opencontainers.image.release="coaching-registration-20261014"
