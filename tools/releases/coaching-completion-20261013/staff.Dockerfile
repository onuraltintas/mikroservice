ARG BASE_IMAGE
FROM ${BASE_IMAGE}
ARG SOURCE_REVISION
COPY payload/staff/ /usr/share/nginx/html/
LABEL org.opencontainers.image.revision="${SOURCE_REVISION}" org.opencontainers.image.release="coaching-completion-20261013"
