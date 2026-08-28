class AuthUser {
  final String userId;
  final String username;
  final String displayName;
  final String role;
  final DateTime expiresAt;

  AuthUser({
    required this.userId,
    required this.username,
    required this.displayName,
    required this.role,
    required this.expiresAt,
  });

  factory AuthUser.fromLoginResponse(Map<String, dynamic> json) {
    return AuthUser(
      userId: json['userId'] as String,
      username: json['username'] as String,
      displayName: json['displayName'] as String,
      role: json['role'] as String,
      expiresAt: DateTime.parse(json['expiresAt'] as String),
    );
  }

  bool get isAdmin => role == 'Admin' || role == 'SuperAdmin';
}
