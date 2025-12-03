FROM openresty/openresty:alpine

# Install additional dependencies
RUN apk add --no-cache curl

# Copy NGINX configuration
COPY nginx.conf /usr/local/openresty/nginx/conf/nginx.conf

# Copy Lua scripts
COPY lua/ /etc/nginx/lua/

# Create log directory
RUN mkdir -p /var/log/nginx

# Expose port
EXPOSE 80

# Health check
HEALTHCHECK --interval=10s --timeout=3s --start-period=5s --retries=3 \
    CMD curl -f http://localhost/health || exit 1

# Start NGINX
CMD ["/usr/local/openresty/bin/openresty", "-g", "daemon off;"]
