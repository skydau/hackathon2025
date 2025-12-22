using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MedDispenseSimulator.Models;
using System.Net.Http.Json;
using System.Text.Json;

namespace MedDispenseSimulator.Services;

/// <summary>
/// 药品配送站模拟器服务
/// </summary>
public class MedDispenseSimulatorService : BackgroundService
{
    private readonly ILogger<MedDispenseSimulatorService> _logger;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly List<DeviceConfig> _devices;
    private readonly Random _random = new();
    
    // 模拟药品数据
    private readonly List<(string Code, string Name, decimal Price)> _medications = new()
    {
        ("MED001", "阿莫西林胶囊", 15.50m),
        ("MED002", "布洛芬片", 12.80m),
        ("MED003", "维生素C片", 8.90m),
        ("MED004", "感冒灵颗粒", 18.60m),
        ("MED005", "头孢克肟胶囊", 32.40m),
        ("MED006", "奥美拉唑肠溶胶囊", 28.70m),
        ("MED007", "氯雷他定片", 22.30m),
        ("MED008", "复方甘草片", 9.80m),
        ("MED009", "硝苯地平缓释片", 45.60m),
        ("MED010", "阿司匹林肠溶片", 16.20m)
    };
    
    // 模拟患者ID
    private readonly List<string> _patientIds = new()
    {
        "P001001", "P001002", "P001003", "P001004", "P001005",
        "P002001", "P002002", "P002003", "P002004", "P002005",
        "P003001", "P003002", "P003003", "P003004", "P003005"
    };
    
    // 模拟操作员ID
    private readonly List<string> _operatorIds = new()
    {
        "OP001", "OP002", "OP003", "OP004", "OP005"
    };

    public MedDispenseSimulatorService(
        ILogger<MedDispenseSimulatorService> logger,
        IConfiguration configuration,
        HttpClient httpClient)
    {
        _logger = logger;
        _configuration = configuration;
        _httpClient = httpClient;
        
        // 配置 HttpClient - 连接到 Smart Gateway
        var baseUrl = _configuration["SmartGateway:BaseUrl"] ?? "http://localhost:30000";
        _httpClient.BaseAddress = new Uri(baseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(_configuration.GetValue<int>("SmartGateway:Timeout", 30));
        
        // 加载设备配置
        _devices = _configuration.GetSection("Devices").Get<List<DeviceConfig>>() ?? new List<DeviceConfig>();
        
        _logger.LogInformation("已加载 {DeviceCount} 个设备配置", _devices.Count);
        foreach (var device in _devices)
        {
            _logger.LogInformation("设备: {SerialNumber} - {Description} (租户: {TenantId})", 
                device.SerialNumber, device.Description, device.TenantId);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("药品配送站模拟器启动");
        
        var intervalSeconds = _configuration.GetValue<int>("Simulation:IntervalSeconds", 5);
        var batchSize = _configuration.GetValue<int>("Simulation:BatchSize", 3);
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SimulateBatchDispense(batchSize);
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "模拟过程中发生错误");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
        
        _logger.LogInformation("药品配送站模拟器停止");
    }

    /// <summary>
    /// 模拟批量配送
    /// </summary>
    private async Task SimulateBatchDispense(int batchSize)
    {
        var tasks = new List<Task>();
        
        for (int i = 0; i < batchSize && i < _devices.Count; i++)
        {
            var device = _devices[_random.Next(_devices.Count)];
            tasks.Add(SimulateDispenseFromDevice(device));
        }
        
        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// 模拟单个设备的配送
    /// </summary>
    private async Task SimulateDispenseFromDevice(DeviceConfig device)
    {
        try
        {
            // 生成配送消息
            var message = GenerateDispenseMessage(device);
            
            _logger.LogInformation("设备 {SerialNumber} 开始配送，患者: {PatientId}，总金额: {Amount:C}",
                device.SerialNumber, message.PatientId, message.TotalAmount);
            
            // 转换为事务请求
            var transactionRequest = new TransactionRequest
            {
                Amount = message.TotalAmount,
                Description = $"药品配送 - 设备: {device.SerialNumber}, 患者: {message.PatientId}, " +
                            $"药品: {string.Join(", ", message.Medications.Select(m => m.MedicationName))}"
            };
            
            // 发送到 Smart Gateway
            await SendTransactionToSmartGateway(device.SerialNumber, transactionRequest);
            
            _logger.LogInformation("设备 {SerialNumber} 配送完成，事务已记录", device.SerialNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "设备 {SerialNumber} 配送失败", device.SerialNumber);
        }
    }

    /// <summary>
    /// 生成配送消息
    /// </summary>
    private MedDispenseMessage GenerateDispenseMessage(DeviceConfig device)
    {
        var message = new MedDispenseMessage
        {
            DeviceSerialNumber = device.SerialNumber,
            TenantId = device.TenantId,
            PatientId = _patientIds[_random.Next(_patientIds.Count)],
            OperatorId = _operatorIds[_random.Next(_operatorIds.Count)],
            DispenseTime = DateTime.UtcNow,
            Notes = $"从 {device.Location} 配送"
        };
        
        // 随机选择 1-4 种药品
        var medicationCount = _random.Next(1, 5);
        var selectedMedications = _medications.OrderBy(x => _random.Next()).Take(medicationCount);
        
        foreach (var (code, name, price) in selectedMedications)
        {
            var quantity = _random.Next(1, 4); // 1-3 盒/瓶
            message.Medications.Add(new MedicationItem
            {
                MedicationCode = code,
                MedicationName = name,
                Quantity = quantity,
                UnitPrice = price
            });
        }
        
        message.TotalAmount = message.Medications.Sum(m => m.Subtotal);
        
        return message;
    }

    /// <summary>
    /// 发送事务到 Smart Gateway
    /// </summary>
    private async Task SendTransactionToSmartGateway(string deviceSerialNumber, TransactionRequest request)
    {
        try
        {
            // 设置设备ID头部，让 Smart Gateway 进行租户识别和路由
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/transactions");
            httpRequest.Headers.Add("Device-Id", deviceSerialNumber);
            httpRequest.Content = JsonContent.Create(request);
            
            var response = await _httpClient.SendAsync(httpRequest);
            
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("事务创建成功，设备: {DeviceId}，响应: {Response}", deviceSerialNumber, responseContent);
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("事务创建失败，设备: {DeviceId}，状态码: {StatusCode}，错误: {Error}",
                    deviceSerialNumber, response.StatusCode, errorContent);
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "网络请求失败，设备: {DeviceId}", deviceSerialNumber);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "请求超时，设备: {DeviceId}", deviceSerialNumber);
        }
    }
}