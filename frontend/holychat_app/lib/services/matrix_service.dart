import 'package:matrix/matrix.dart';
import 'package:path_provider/path_provider.dart';
import 'package:sqflite_common_ffi/sqflite_ffi.dart' as sqflite_ffi;
import 'package:flutter_vodozemac/flutter_vodozemac.dart' as vod;
import 'package:flutter/foundation.dart' show kIsWeb, defaultTargetPlatform, TargetPlatform, compute;
import 'package:sqflite/sqflite.dart' as sqflite;
import '../app_config.dart';

/// Envolve o Client do SDK Matrix (Famedly) - liga ao Synapse,
/// gere login/logout e sessão local persistente.
///
/// A sessão (incluindo chaves de encriptação) fica guardada numa
/// base de dados SQLite local ao dispositivo, gerida inteiramente
/// pelo SDK. Depois do primeiro login bem-sucedido, reabrir a app
/// restaura a sessão automaticamente via client.init() - não é
/// preciso fazer login outra vez enquanto a sessão for válida.
class MatrixService {
  Client? _client;
  bool _initialized = false;

  Client get client {
    if (_client == null) {
      throw StateError('MatrixService not initialized - call init() first.');
    }
    return _client!;
  }

  bool get isLoggedIn => _client?.isLogged() ?? false;

  /// Confirma que a sessão Matrix ativa (se houver) pertence ao
  /// username indicado - importante porque uma sessão anterior pode
  /// ter ficado persistida na base de dados local do dispositivo.
  bool isLoggedInAs(String username) {
    if (!isLoggedIn) return false;
    final userId = client.userID;
    if (userId == null) return false;
    return userId.toLowerCase().startsWith('@${username.toLowerCase()}:');
  }

  Future<void> init() async {
    if (_initialized) return;

    await vod.init();

    final dbDirectory = await getApplicationSupportDirectory();

    // Desktop (Windows/Linux) usa a factory FFI do sqflite;
    // Android/iOS usam o sqflite normal, que já vem com binding nativo.
    final isDesktop = !kIsWeb &&
        (defaultTargetPlatform == TargetPlatform.windows ||
            defaultTargetPlatform == TargetPlatform.linux ||
            defaultTargetPlatform == TargetPlatform.macOS);

    final dbFactory = isDesktop ? sqflite_ffi.databaseFactoryFfi : sqflite.databaseFactory;

    final database = await MatrixSdkDatabase.init(
      'holychat_matrix',
      database: await dbFactory.openDatabase('${dbDirectory.path}/holychat_matrix.sqlite'),
      sqfliteFactory: dbFactory,
    );

    _client = Client(
      'HolyChat',
      database: database,
      nativeImplementations: NativeImplementationsIsolate(
        compute,
        vodozemacInit: () => vod.init(),
      ),
    );

    // Restaura sessão existente, se houver (não faz login novo).
    await _client!.init();
    _initialized = true;
  }

  /// Faz login com username+password - a mesma password usada no
  /// backend HolyChat, já que ambos os sistemas partilham a mesma
  /// conta Matrix como fonte de verdade (decisão tomada no design).
  Future<void> login(String username, String password) async {
    await client.checkHomeserver(Uri.parse(AppConfig.matrixHomeserverUrl));
    await client.login(
      LoginType.mLoginPassword,
      identifier: AuthenticationUserIdentifier(user: username),
      password: password,
    );
  }

  Future<void> logout() async {
    if (isLoggedIn) {
      await client.logout();
    }
  }
}
