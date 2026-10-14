ARG BASE_IMAGE
FROM ${BASE_IMAGE}
ARG SOURCE_REVISION
USER root
RUN rm -rf /usr/share/nginx/html/*
COPY payload/frontend/ /usr/share/nginx/html/
RUN chmod -R u=rwX,go=rX /usr/share/nginx/html
LABEL org.opencontainers.image.revision="${SOURCE_REVISION}" org.opencontainers.image.release="master-login-20261014"
