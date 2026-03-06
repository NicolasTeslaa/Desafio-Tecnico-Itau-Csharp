FROM node:20-alpine AS build
WORKDIR /app

COPY spa/package.json spa/package-lock.json ./
RUN npm ci

COPY spa/ ./

ARG VITE_SCHEDULED_BASE_URL
ARG VITE_MARKETDATA_BASE_URL
ENV VITE_SCHEDULED_BASE_URL=$VITE_SCHEDULED_BASE_URL
ENV VITE_MARKETDATA_BASE_URL=$VITE_MARKETDATA_BASE_URL

RUN npm run build

FROM nginx:1.27-alpine AS runtime
COPY spa/nginx.default.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html

EXPOSE 80
