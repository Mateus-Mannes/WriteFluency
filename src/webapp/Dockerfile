FROM node:22-alpine AS builder

WORKDIR /app

COPY package*.json ./
RUN npm install

COPY . .
RUN npm run build:prod

FROM node:22-alpine

WORKDIR /app

# Copy package files and install production dependencies only
COPY package*.json ./
RUN npm ci --omit=dev

# Copy the built application (both browser and server bundles)
COPY --from=builder /app/dist/write-fluency-app ./dist/write-fluency-app

# Set the port for the SSR server
ENV PORT=4200

EXPOSE 4200

# Run the SSR server
CMD ["node", "dist/write-fluency-app/server/server.mjs"]