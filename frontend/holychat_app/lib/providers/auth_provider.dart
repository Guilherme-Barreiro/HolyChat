import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../models/auth_user.dart';
import '../services/api_service.dart';
import '../services/secure_storage_service.dart';
import 'matrix_provider.dart';

final apiServiceProvider = Provider<ApiService>((ref) => ApiService());
final secureStorageProvider = Provider<SecureStorageService>((ref) => SecureStorageService());

/// Estado de autenticação: ou não há sessão (user == null),
/// ou há uma sessão ativa com o utilizador atual.
class AuthState {
  final AuthUser? user;
  final String? token;
  final bool isLoading;

  const AuthState({this.user, this.token, this.isLoading = false});

  bool get isAuthenticated => user != null && token != null;

  AuthState copyWith({AuthUser? user, String? token, bool? isLoading}) {
    return AuthState(
      user: user ?? this.user,
      token: token ?? this.token,
      isLoading: isLoading ?? this.isLoading,
    );
  }
}

/// Riverpod 3.x: Notifier substitui o antigo StateNotifier.
/// Não tem construtor - o `ref` já vem disponível automaticamente
/// como propriedade da classe base, e o estado inicial define-se
/// no método build().
class AuthNotifier extends Notifier<AuthState> {
  @override
  AuthState build() => const AuthState();

  Future<void> login({
    required String username,
    required String password,
    String? totpCode,
  }) async {
    state = state.copyWith(isLoading: true);
    try {
      final api = ref.read(apiServiceProvider);
      final storage = ref.read(secureStorageProvider);
      final matrix = ref.read(matrixServiceProvider);

      // 1. Autentica no backend HolyChat (JWT).
      final result = await api.login(
        username: username,
        password: password,
        totpCode: totpCode,
      );
      await storage.saveToken(result.token);

      // 2. Autentica também no Matrix, com as mesmas credenciais -
      // uma password só, como decidido. Se já houver uma sessão
      // Matrix persistida localmente MAS de outro utilizador (ex:
      // trocaste de conta no mesmo dispositivo/emulador), termina-a
      // primeiro antes de autenticar a conta certa.
      await matrix.init();
      if (matrix.isLoggedIn && !matrix.isLoggedInAs(username)) {
        await matrix.logout();
      }
      if (!matrix.isLoggedIn) {
        await matrix.login(username, password);
      }

      state = AuthState(user: result.user, token: result.token, isLoading: false);
    } catch (e) {
      state = state.copyWith(isLoading: false);
      rethrow;
    }
  }

  Future<void> logout() async {
    final storage = ref.read(secureStorageProvider);
    final matrix = ref.read(matrixServiceProvider);
    await storage.deleteToken();
    await matrix.logout();
    state = const AuthState();
  }
}

final authProvider = NotifierProvider<AuthNotifier, AuthState>(AuthNotifier.new);
