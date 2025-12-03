-- rate_limiter.lua
-- Implements tenant-level rate limiting based on X-Tenant-Id header

local cjson = require "cjson"
local http = require "resty.http"

-- Get tenant ID from context (set by tenant_router.lua)
local tenant_id = ngx.var.tenant_id

-- Default rate limit (requests per second)
local default_rps = 50
local burst_size = 100

-- If no tenant ID, apply default rate limit
if not tenant_id or tenant_id == "" then
    tenant_id = "default"
end

-- Get shared dictionary for rate limiting
local limit_dict = ngx.shared.tenant_rate_limit

-- Rate limiting key
local rate_key = "rate:" .. tenant_id
local config_key = "config:" .. tenant_id
local last_check_key = "last_check:" .. tenant_id

-- Check if we need to fetch tenant configuration
local tenant_rps = limit_dict:get(config_key)
local last_check = limit_dict:get(last_check_key)
local now = ngx.now()

-- Refresh config every 60 seconds
if not tenant_rps or not last_check or (now - last_check) > 60 then
    -- Fetch tenant configuration from Tenant Catalog
    local httpc = http.new()
    httpc:set_timeout(2000)  -- 2 second timeout
    
    local catalog_url = "http://tenant-catalog-service.default.svc.cluster.local:8080/api/tenants/" 
        .. ngx.escape_uri(tenant_id)
    
    local res, err = httpc:request_uri(catalog_url, {
        method = "GET",
        headers = {
            ["Content-Type"] = "application/json",
        }
    })
    
    if res and res.status == 200 then
        local ok, data = pcall(cjson.decode, res.body)
        if ok and data.throttling and data.throttling.rps then
            tenant_rps = data.throttling.rps
            limit_dict:set(config_key, tenant_rps, 120)  -- Cache for 2 minutes
            ngx.log(ngx.INFO, "Updated rate limit for tenant ", tenant_id, ": ", tenant_rps, " rps")
        end
    end
    
    limit_dict:set(last_check_key, now, 120)
end

-- Use default if no specific config found
if not tenant_rps then
    tenant_rps = default_rps
end

-- Token bucket algorithm implementation
local current_tokens = limit_dict:get(rate_key)
local last_update_key = "last_update:" .. tenant_id
local last_update = limit_dict:get(last_update_key) or now

-- Calculate tokens to add based on time elapsed
local time_elapsed = now - last_update
local tokens_to_add = time_elapsed * tenant_rps

if not current_tokens then
    current_tokens = burst_size
else
    current_tokens = math.min(burst_size, current_tokens + tokens_to_add)
end

-- Check if we have tokens available
if current_tokens < 1 then
    ngx.log(ngx.WARN, "Rate limit exceeded for tenant ", tenant_id)
    ngx.status = 429
    ngx.header.content_type = "application/json"
    ngx.header["Retry-After"] = "1"
    ngx.header["X-RateLimit-Limit"] = tostring(tenant_rps)
    ngx.header["X-RateLimit-Remaining"] = "0"
    ngx.say(cjson.encode({
        error = {
            code = "RATE_LIMIT_EXCEEDED",
            message = "Too many requests. Please try again later.",
            tenantId = tenant_id,
            limit = tenant_rps,
            timestamp = now
        }
    }))
    return ngx.exit(429)
end

-- Consume one token
current_tokens = current_tokens - 1
limit_dict:set(rate_key, current_tokens, 10)
limit_dict:set(last_update_key, now, 10)

-- Add rate limit headers to response
ngx.header["X-RateLimit-Limit"] = tostring(tenant_rps)
ngx.header["X-RateLimit-Remaining"] = tostring(math.floor(current_tokens))
