# Auricrux App - Production-Ready .NET/C# Application

A complete, enterprise-grade AI assistant application built with .NET 10, featuring cross-platform support through MAUI and web deployment via Blazor Server.

## Features

- **Multi-platform support**: MAUI for Android, iOS, Windows, and macOS
- **Web deployment**: Blazor Server for browser-based access
- **AI integration**: Seamless communication with Auricrux backend API
- **Advanced chat interface**:
  - Multiple thinking modes (Quick, Auto, Deep) for flexible AI responses
  - Configurable search scopes (Internal, Public, Both)
  - Auto-speak/TTS support for accessibility
  - Real-time conversation history
  - Star rating feedback system for response quality
- **Production-ready code**:
  - Full error handling and logging
  - Async/await patterns throughout
  - Dependency injection and IoC containers
  - Shared service layer for code reuse

## Project Structure

```
auricrux-app/
├── Auricrux.Shared/          # Shared class library
│   ├── Models.cs             # Data models, enums (ThinkingMode, SearchScope, etc.)
│   └── Services.cs           # API client, TTS, business logic
├── Auricrux.Mobile/          # MAUI cross-platform app
│   ├── MainPage.xaml         # UI layout
│   ├── MainPageViewModel.cs  # MVVM logic
│   ├── MauiProgram.cs        # Service registration & configuration
│   └── Resources/            # Images, fonts, assets
├── Auricrux.Web/             # Blazor Server web app
│   ├── Components/
│   │   ├── Pages/Chat.razor  # Main chat interface
│   │   └── Layout/           # App shell
│   ├── Program.cs            # Web app configuration
│   └── appsettings.json      # Configuration
├── AuricruxApp.sln           # Solution file
├── README.md                 # This file
└── .gitignore                # Git exclusions
```

## Prerequisites

- **.NET 10 SDK** or later ([Download](https://dotnet.microsoft.com/download))
- **Visual Studio 2024** (or VS Code with C# extension) - recommended for MAUI development
- **Mobile development tools** (optional):
  - Android SDK/emulator (for Android development)
  - Xcode (for iOS development)
  - Visual Studio with MAUI workload installed

## Quick Start

### 1. Clone/Setup

```bash
cd C:\Users\Auricrux\OneDrive\FCA\auricrux-app
dotnet restore
```

### 2. Run the Blazor Web App

```bash
cd Auricrux.Web
dotnet run
```

The web app will be available at `https://localhost:7000`

### 3. Build and Run MAUI (Windows Desktop)

```bash
cd Auricrux.Mobile
dotnet build -f net10.0-windows10.0.19041.0
dotnet run -f net10.0-windows10.0.19041.0
```

### 4. Build for Android (if Android SDK installed)

```bash
cd Auricrux.Mobile
dotnet publish -f net10.0-android -c Release
```

## Configuration

### Backend API Endpoint

The default backend endpoint is `http://localhost:5000`. To change it:

**MAUI (MainPage.xaml)**:
```csharp
var auricruxConfig = new AuricruxConfig
{
    ApiEndpoint = "https://your-api.com",
    // ...
};
```

**Blazor Web (appsettings.json)**:
```json
{
  "Auricrux": {
    "ApiEndpoint": "https://your-api.com"
  }
}
```

### Thinking Modes

The app supports three thinking modes:
- **Quick**: Minimal reasoning, fastest response
- **Auto**: Balanced thinking time and response quality (default)
- **Deep**: Extended thinking for complex queries

### Search Scopes

- **Internal**: Search only internal documents/knowledge base
- **Public**: Search public resources only
- **Both**: Combined search (default)

## API Requirements

The backend must provide these endpoints:

### POST /api/chat
Send a chat query and receive an AI-generated response.

**Request**:
```json
{
  "query": "What is Auricrux?",
  "thinkingMode": "Auto",
  "searchScope": "Both",
  "conversationHistory": [],
  "sessionId": "session-uuid"
}
```

**Response**:
```json
{
  "content": "Auricrux is an AI assistant...",
  "thinkingContent": "Internal reasoning...",
  "sources": [
    {
      "title": "Source 1",
      "url": "https://...",
      "relevanceScore": 0.95
    }
  ],
  "timestamp": "2025-01-15T10:30:00Z",
  "processingTimeMs": 1234,
  "confidenceScore": 0.92
}
```

### GET /health
Health check endpoint to verify backend availability.

**Response**:
```json
{
  "status": "healthy",
  "uptime": 12345
}
```

### POST /api/feedback/{interactionId}
Submit user feedback/rating for an interaction.

**Request**:
```json
{
  "stars": 5,
  "comment": "Great response!",
  "timestamp": "2025-01-15T10:35:00Z"
}
```

## Building for Production

### Blazor Web Deployment

**Docker**:
```bash
cd Auricrux.Web
docker build -t auricrux-web:1.0 .
docker run -p 80:8080 -p 443:8443 auricrux-web:1.0
```

**Azure App Service**:
```bash
cd Auricrux.Web
dotnet publish -c Release -o ./publish
# Upload to Azure App Service
```

**Self-hosted (IIS)**:
```bash
cd Auricrux.Web
dotnet publish -c Release -o ./publish
# Deploy to IIS as an ASP.NET Core application
```

### MAUI Mobile Deployment

**Android APK**:
```bash
cd Auricrux.Mobile
dotnet publish -f net10.0-android -c Release -p:AndroidPackageFormat=apk
# APK output: bin/Release/net10.0-android/com.companyname.auricrux.mobile-Signed.apk
```

**iOS App**:
```bash
cd Auricrux.Mobile
dotnet publish -f net10.0-ios -c Release
# Follow App Store submission process
```

**macOS App**:
```bash
cd Auricrux.Mobile
dotnet publish -f net10.0-maccatalyst -c Release
```

## Development

### Build All Projects

```bash
dotnet build
```

### Run Unit Tests (if added)

```bash
dotnet test
```

### Code Style & Analysis

Using C# 12 features with nullable reference types enabled for type safety.

```bash
dotnet format  # if Roslyn analyzers are configured
```

## Troubleshooting

### "Cannot connect to backend"
- Verify backend is running at the configured endpoint
- Check network connectivity and firewall settings
- Ensure `ApiEndpoint` is correctly configured

### "MAUI build fails"
- Ensure .NET 10 SDK is installed: `dotnet --version`
- For platform-specific builds, verify appropriate SDKs (Android SDK, Xcode, etc.)
- Clean and rebuild: `dotnet clean && dotnet restore && dotnet build`

### "Blazor components not rendering"
- Check browser console for JavaScript errors
- Verify Auricrux services are registered in `Program.cs`
- Ensure `@rendermode InteractiveServer` is set on interactive components

## Logging

All services include comprehensive logging:

**MAUI**: Output to Debug Console in Debug mode
```csharp
builder.Logging.AddDebug();
```

**Blazor Web**: Output to console and application logs
```csharp
logging.AddConsole();
```

Log level can be configured in `appsettings.json`.

## Architecture

### Shared Library (Auricrux.Shared)
- **Models**: Enums, DTOs, configuration objects
- **Services**:
  - `AuricruxApiClient`: HTTP communication with backend
  - `TextToSpeechService`: Platform-agnostic TTS wrapper
  - `AuricruxService`: Business logic, conversation management

### MAUI App (Auricrux.Mobile)
- XAML-based UI for cross-platform mobile/desktop
- MVVM pattern with `MainPageViewModel`
- Async/await for responsive UI
- Platform-specific integrations (permissions, TTS)

### Blazor Web (Auricrux.Web)
- Server-side rendering with SignalR
- Bootstrap 5 responsive design
- Razor components for composable UI
- Real-time interactivity

## Security Considerations

1. **API Communication**: Use HTTPS in production
2. **Authentication**: Implement OAuth2/OpenID Connect as needed
3. **Data Validation**: All inputs validated before sending to backend
4. **Sensitive Data**: Avoid storing tokens in app.config/appsettings
5. **CORS**: Configure backend CORS policies appropriately

## Performance Optimization

- **Async Operations**: All long-running operations use async/await
- **Message History**: Limited to 10 most recent messages per request
- **Lazy Loading**: UI components load on-demand
- **HTTP Client**: Reusable HttpClient via dependency injection
- **Caching**: Consider implementing caching for frequently requested data

## Future Enhancements

- [ ] Message persistence (local SQLite database)
- [ ] Conversation export (PDF/TXT)
- [ ] Image/file upload support
- [ ] Voice input transcription
- [ ] Advanced analytics dashboard
- [ ] Multi-language support
- [ ] Offline mode with sync

## License

This project is part of Future Contractors of America LLC.

## Support

For issues or questions, contact the development team or submit an issue in the repository.

---

**Version**: 1.0.0  
**Last Updated**: 2025-01-15  
**Built with**: .NET 10, MAUI, Blazor Server
