# Security

Identity hashes passwords with its configured password hasher and temporarily locks an account after five failed attempts. Login is rate limited. The API uses a configured Base64 signing key of at least 32 bytes. Refresh credentials are random and stored as SHA-256 hashes. SQL rowversion makes a rotation single-use. Session validation rejects disabled, expired, rotated-out and revoked credentials as appropriate. The refresh cookie has HttpOnly and SameSite=Strict, and Secure in production. Origin and a custom header protect cookie operations.

Administrative role and lock changes update the security stamp. User and role administration remain restricted to Administrator. Other role grants can be changed independently. Permissions are resolved from SQL Server on protected routes. Administrative accounts cannot be locked or reassigned through the ordinary user editor.

Development uses a local generated key and password under the ignored .work directory. Production uses external secrets and a separate provisioning command. The UI holds access tokens in memory and clears cached server data when the user changes. The confirmation dialog is separate from server-side authorization.

Responses expose a correlation ID for investigation. Login and changes to account access write audit records with bounded change details. No password, JWT, refresh credential or connection string belongs in audit records.

Current verification and unresolved checks are listed in [test report](TEST_REPORT.md) and [known issues](KNOWN_ISSUES.md).
