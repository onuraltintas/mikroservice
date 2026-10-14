ARG BASE_IMAGE
FROM ${BASE_IMAGE}
ARG SOURCE_REVISION
COPY payload/speed-reading/ /usr/share/nginx/html/
LABEL org.opencontainers.image.revision="${SOURCE_REVISION}" org.opencontainers.image.release="multi-institution-enrollment-20261014"
