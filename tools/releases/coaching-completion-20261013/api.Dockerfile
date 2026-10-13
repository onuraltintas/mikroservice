ARG BASE_IMAGE
FROM ${BASE_IMAGE}
ARG SERVICE
ARG SOURCE_REVISION
COPY payload/${SERVICE}/ /app/
LABEL org.opencontainers.image.revision="${SOURCE_REVISION}" org.opencontainers.image.release="coaching-completion-20261013"
