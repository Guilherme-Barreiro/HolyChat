import 'dart:convert';
import 'package:http/http.dart' as http;
import '../app_config.dart';
import '../models/auth_user.dart';

/// Erro genérico de API, com a mensagem que o backend devolveu.
class ApiException implements Exception {
  final String message;
  final int? statusCode;
  ApiException(this.message, {this.statusCode});

  @override
  String toString() => message;
}

/// Erro específico: conta tem 2FA ativo, falta o totpCode.
class TotpRequiredException implements Exception {}

class LoginResult {
  final String token;
  final AuthUser user;
  LoginResult({required this.token, required this.user});
}

class ApiService {
  final String _baseUrl = AppConfig.apiBaseUrl;

  Future<LoginResult> login({
    required String username,
    required String password,
    String? totpCode,
  }) async {
    final response = await http.post(
      Uri.parse('$_baseUrl/api/auth/login'),
      headers: {'Content-Type': 'application/json'},
      body: jsonEncode({
        'username': username,
        'password': password,
        if (totpCode != null && totpCode.isNotEmpty) 'totpCode': totpCode,
      }),
    );

    final body = jsonDecode(response.body) as Map<String, dynamic>;

    if (response.statusCode == 200) {
      return LoginResult(
        token: body['token'] as String,
        user: AuthUser.fromLoginResponse(body),
      );
    }

    final error = body['error'] as String?;

    if (response.statusCode == 401 && error == 'totp_required') {
      throw TotpRequiredException();
    }

    throw ApiException(
      error ?? 'Login failed.',
      statusCode: response.statusCode,
    );
  }
}
