import 'package:flutter_secure_storage/flutter_secure_storage.dart';

/// Guarda o JWT do backend HolyChat em armazenamento encriptado
/// (Keychain no iOS, Keystore/EncryptedSharedPreferences no Android).
/// Nunca usar SharedPreferences para isto - não é encriptado.
class SecureStorageService {
  static const _tokenKey = 'holychat_jwt';

  final _storage = const FlutterSecureStorage();

  Future<void> saveToken(String token) async {
    await _storage.write(key: _tokenKey, value: token);
  }

  Future<String?> readToken() async {
    return _storage.read(key: _tokenKey);
  }

  Future<void> deleteToken() async {
    await _storage.delete(key: _tokenKey);
  }
}
