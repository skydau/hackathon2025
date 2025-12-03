-- tenant_router.lua
-- Extracts device ID from request, queries Device Registry, and injects X-Tenant-Id header

local http = require "resty.http"
local cjson = require "cjson"

-- Extract device ID from request header
local device_id = ngx.var.http_device_id

if not device_id or device_id == "" then
    ngx.log(ngx.ERR, "Missing Device-Id header")
    ngx.status = 400
    ngx.header.content_type = "application/json"
    ngx.say(cjson.encode({
        error = {
            code = "MISSING_DEVICE_ID",
            message = "Device-Id header is required",
            timestamp = ngx.now()
        }
    }))
    return ngx.exit(400)
end

-- Query Device Registry for tenant ID
local httpc = http.new()
httpc:set_timeout(5000)  -- 5 second timeout

local device_registry_url = "http://device-registry-service.default.svc.cluster.local:8080/api/devices/" 
    .. ngx.escape_uri(device_id) .. "/tenant"

local res, err = httpc:request_uri(device_registry_url, {
    method = "GET",
    headers = {
        ["Content-Type"] = "application/json",
    }
})

if not res then
    ngx.log(ngx.ERR, "Failed to query Device Registry: ", err)
    ngx.status = 503
    ngx.header.content_type = "application/json"
    ngx.say(cjson.encode({
        error = {
            code = "SERVICE_UNAVAILABLE",
            message = "Device Registry service is unavailable",
            timestamp = ngx.now()
        }
    }))
    return ngx.exit(503)
end

if res.status == 404 then
    ngx.log(ngx.WARN, "Device not found: ", device_id)
    ngx.status = 401
    ngx.header.content_type = "application/json"
    ngx.say(cjson.encode({
        error = {
            code = "DEVICE_NOT_AUTHORIZED",
            message = "Device is not registered or authorized",
            timestamp = ngx.now()
        }
    }))
    return ngx.exit(401)
end

if res.status ~= 200 then
    ngx.log(ngx.ERR, "Device Registry returned error: ", res.status, " ", res.body)
    ngx.status = 502
    ngx.header.content_type = "application/json"
    ngx.say(cjson.encode({
        error = {
            code = "UPSTREAM_ERROR",
            message = "Failed to retrieve device information",
            timestamp = ngx.now()
        }
    }))
    return ngx.exit(502)
end

-- Parse response and extract tenant ID
local ok, data = pcall(cjson.decode, res.body)
if not ok or not data.tenantId then
    ngx.log(ngx.ERR, "Invalid response from Device Registry: ", res.body)
    ngx.status = 502
    ngx.header.content_type = "application/json"
    ngx.say(cjson.encode({
        error = {
            code = "INVALID_RESPONSE",
            message = "Invalid response from Device Registry",
            timestamp = ngx.now()
        }
    }))
    return ngx.exit(502)
end

-- Inject tenant ID into request context
ngx.var.tenant_id = data.tenantId
ngx.req.set_header("X-Tenant-Id", data.tenantId)

ngx.log(ngx.INFO, "Device ", device_id, " mapped to tenant ", data.tenantId)
