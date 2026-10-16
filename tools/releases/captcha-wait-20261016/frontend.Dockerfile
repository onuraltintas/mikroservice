ARG BASE_IMAGE
FROM ${BASE_IMAGE}
ARG SOURCE_REVISION
USER root
RUN rm -f /usr/share/nginx/html/index.html /usr/share/nginx/html/index.html.gz /usr/share/nginx/html/index.html.br /usr/share/nginx/html/ngsw.json
COPY payload/frontend/ /usr/share/nginx/html/
RUN chmod -R u=rwX,go=rX /usr/share/nginx/html
LABEL org.opencontainers.image.revision="${SOURCE_REVISION}" org.opencontainers.image.release="captcha-wait-20261016"
