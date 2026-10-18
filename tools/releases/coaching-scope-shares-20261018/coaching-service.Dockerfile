ARG BASE_IMAGE
FROM ${BASE_IMAGE}
ARG SOURCE_REVISION
LABEL org.opencontainers.image.revision=$SOURCE_REVISION
COPY payload/coaching/ /app/
