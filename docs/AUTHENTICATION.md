# Authentication Implementation

## Overview

The WiseSub API implements OAuth 2.0 authentication with Google and JWT-based API authentication. This document describes the authentication flow and configuration.

## Architecture

### Components

1. **AuthController** (`src/WiseSub.API/Controllers/AuthController.cs`)
   - Handles authentication endpoints
   - Manages OAuth callback and token refresh
   - Provides user profile endpoint

2. **GoogleAuthenticationService** (`src/WiseSub.Infrastructure/Authentication/GoogleAuthenticationService.cs`)
   - Implements OAuth 2.0 flow with Google
   - Exchanges authorization codes for access tokens
   - Generates JWT tokens for API authentication

3. **UserService** (`src/WiseSub.Application/Services/UserService.cs`)
   - Manages user CRUD operations
   - Handles user data export (GDPR compliance)
   - Manages user data deletion

## Authentication Flow

### 1. Google OAuth Flow

```
Frontend                    Backend                     Google
   |                           |                           |
   |-- GET /auth/google ------>|                           |
   |                           |-- Redirect to Google ---->|
   |                           |                           |
   |<------------------------- Authorization Page ---------|
   |                           |                           |
   |-- Authorization Code ---->|                           |
   |                           |-- Exchange Code --------->|
   |                           |<-- Access Token ----------|
   |                           |                           |
   |                           |-- Get User Info --------->|
   |                           |<-- User Profile ----------|
   |                           |                           |
   |<-- JWT Token -------------|                           |
```

### 2. API Authentication

All protected endpoints require a JWT token in the Authorization header:

```
Authorization: Bearer <jwt_token>
```

## API Endpoints

### POST /api/auth/google

Authenticates a user with Google OAuth.

**Request:**
```json
{
  "authorizationCode": "4/0AY0e-g7..."
}
```

**Response:**
```json
{
  "userId": "user-guid",
  "email": "user@example.com",
  "token": "eyJhbGciOiJIUzI1NiIs...",
  "refreshToken": "1//0g...",
  "isNewUser": true
}
```

### POST /api/auth/refresh

Refreshes an expired JWT token.

**Request:**
```json
{
  "refreshToken": "1//0g..."
}
```

**Response:**
```json
{
  "userId": "user-guid",
  "email": "user@example.com",
  "token": "eyJhbGciOiJIUzI1NiIs...",
  "refreshToken": "1//0g..."
}
```

### POST /api/auth/logout

Logs out the current user (client-side token removal).

**Headers:**
```
Authorization: Bearer <jwt_token>
```

**Response:**
```json
{
  "message": "Logged out successfully"
}
```

### GET /api/auth/me

Gets the current authenticated user's profile.

**Headers:**
```
Authorization: Bearer <jwt_token>
```

**Response:**
```json
{
  "id": "user-guid",
  "email": "user@example.com",
  "name": "John Doe",
  "tier": "Free",
  "createdAt": "2025-11-22T10:00:00Z",
  "lastLoginAt": "2025-11-22T10:00:00Z"
}
```

## Configuration

### Required Settings

Add the following to `appsettings.json`:

```json
{
  "Authentication": {
    "JwtSecret": "YOUR_SECRET_KEY_AT_LEAST_32_CHARACTERS",
    "JwtIssuer": "WiseSub",
    "JwtAudience": "WiseSub",
    "Google": {
      "ClientId": "YOUR_GOOGLE_CLIENT_ID",
      "ClientSecret": "YOUR_GOOGLE_CLIENT_SECRET",
      "RedirectUri": "http://localhost:3000/auth/callback"
    }
  }
}
```

### Google OAuth Setup

1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Create a new project or select an existing one
3. Enable the Google+ API
4. Create OAuth 2.0 credentials:
   - Application type: Web application
   - Authorized redirect URIs: `http://localhost:3000/auth/callback`
5. Copy the Client ID and Client Secret to your configuration

### JWT Configuration

For production, generate a secure JWT secret:

```bash
# Generate a random 32-byte key
openssl rand -base64 32
```

## Security Considerations

### Token Storage

- **Access Tokens**: Stored encrypted in the database using AES-256
- **JWT Tokens**: Stored client-side (localStorage or httpOnly cookies)
- **Refresh Tokens**: Stored client-side for token refresh

### Token Expiration

- **JWT Tokens**: 24 hours
- **Google Access Tokens**: 1 hour (automatically refreshed)
- **Google Refresh Tokens**: No expiration (until revoked)

### Best Practices

1. **Never commit secrets** to version control
2. Use **environment variables** or **Azure Key Vault** for production
3. Implement **token rotation** for refresh tokens
4. Use **HTTPS** in production
5. Implement **rate limiting** on authentication endpoints
6. Log all **authentication attempts** for security monitoring

## User Management

### Creating Users

Users are automatically created on first login via OAuth. Default settings:

- **Tier**: Free
- **Email Accounts**: 0 (must connect)
- **Subscriptions**: 0 (discovered from emails)
- **Preferences**: Default alert settings enabled

### User Data Export (GDPR)

Users can export their data via the UserService:

```csharp
var exportData = await _userService.ExportUserDataAsync(userId);
```

Returns a JSON file containing:
- User profile
- Email accounts
- Subscriptions
- Alerts

### User Data Deletion (Right to be Forgotten)

Complete user data deletion:

```csharp
await _userService.DeleteUserDataAsync(userId);
```

Deletes:
- User profile
- Email accounts and OAuth tokens
- Subscriptions and history
- Alerts
- Email metadata

## Testing

### Unit Tests

User service tests are located in:
- `tests/WiseSub.Infrastructure.Tests/Services/UserServiceTests.cs`

Run tests:
```bash
dotnet test
```

### Manual Testing

1. Start the API:
   ```bash
   dotnet run --project src/WiseSub.API
   ```

2. Test authentication endpoint:
   ```bash
   curl -X POST http://localhost:5000/api/auth/google \
     -H "Content-Type: application/json" \
     -d '{"authorizationCode": "YOUR_AUTH_CODE"}'
   ```

3. Test protected endpoint:
   ```bash
   curl -X GET http://localhost:5000/api/auth/me \
     -H "Authorization: Bearer YOUR_JWT_TOKEN"
   ```

## Troubleshooting

### Common Issues

1. **"JWT secret not configured"**
   - Ensure `Authentication:JwtSecret` is set in appsettings.json

2. **"Failed to exchange authorization code"**
   - Verify Google OAuth credentials are correct
   - Check redirect URI matches Google Console configuration

3. **"User not found"**
   - User may have been deleted
   - Check database for user record

4. **"Token expired"**
   - Use refresh token endpoint to get a new JWT token

## Future Enhancements

- [ ] Implement token blacklisting for logout
- [ ] Add support for Microsoft OAuth
- [ ] Implement 2FA (Two-Factor Authentication)
- [ ] Add session management
- [ ] Implement device tracking
- [ ] Add OAuth token refresh automation
