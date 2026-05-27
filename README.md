Платформа для автоматизированного составления расписаний.

Локальный запуск (Предварительно настроить appsettings.json):
```bash
# Backend
cd ./src/Unischeduler.Api
dotnet run

# Frontend
cd ./frontend/unischeduler-ui
ng serve --host 0.0.0.0
```
Пример настроек appsettings.json: 
```json
{
  "AllowedHosts": "*",
  "App": {
    "BaseUrl": "http://localhost:4200"
  },
  "ConnectionStrings": {
    "DefaultConnection": "connection-string"
  },
  "YandexSuggestApiKey": "api-key",
  "JwtSettings": {
    "Secret": "32-chars-long-secret_AAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
    "ExpiryDays": 7,
    "Issuer": "UniScheduler",
    "Audience": "UniSchedulerApp"
  },
  "EmailSettings": {
    "Provider": "Resend", // Console, Smtp supported
    "FromAddress": "noreply@address.com",
    "FromName": "Юниран",
    "Smtp": {
      "Host": "",
      "Port": 587,
      "Username": "",
      "Password": "",
      "UseStartTls": true
    },
    "Resend": {
      "ApiKey": "api-key"
    }
  }
}
```
