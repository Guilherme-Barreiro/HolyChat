import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../providers/auth_provider.dart';
import '../services/api_service.dart';

class LoginScreen extends ConsumerStatefulWidget {
  const LoginScreen({super.key});

  @override
  ConsumerState<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends ConsumerState<LoginScreen> {
  final _usernameController = TextEditingController();
  final _passwordController = TextEditingController();
  final _totpController = TextEditingController();

  bool _totpRequired = false;
  String? _errorMessage;

  @override
  void dispose() {
    _usernameController.dispose();
    _passwordController.dispose();
    _totpController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    setState(() => _errorMessage = null);

    try {
      await ref.read(authProvider.notifier).login(
            username: _usernameController.text.trim(),
            password: _passwordController.text,
            totpCode: _totpRequired ? _totpController.text.trim() : null,
          );
      // Login bem-sucedido - o AuthState muda, e o widget que decide
      // a navegação (fora deste ecrã) trata do resto.
    } on TotpRequiredException {
      setState(() => _totpRequired = true);
    } on ApiException catch (e) {
      setState(() => _errorMessage = e.message);
    } catch (e, stack) {
      // ignore: avoid_print
      print('LOGIN ERROR: $e');
      // ignore: avoid_print
      print('STACK: $stack');
      setState(() => _errorMessage = 'Something went wrong: $e');
    }
  }

  @override
  Widget build(BuildContext context) {
    final authState = ref.watch(authProvider);

    return Scaffold(
      body: SafeArea(
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(24),
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 400),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Text(
                    'HolyChat',
                    style: Theme.of(context).textTheme.headlineMedium,
                    textAlign: TextAlign.center,
                  ),
                  const SizedBox(height: 32),
                  TextField(
                    controller: _usernameController,
                    enabled: !_totpRequired,
                    decoration: const InputDecoration(
                      labelText: 'Username',
                      border: OutlineInputBorder(),
                    ),
                    textInputAction: TextInputAction.next,
                  ),
                  const SizedBox(height: 16),
                  TextField(
                    controller: _passwordController,
                    enabled: !_totpRequired,
                    obscureText: true,
                    decoration: const InputDecoration(
                      labelText: 'Password',
                      border: OutlineInputBorder(),
                    ),
                    textInputAction: TextInputAction.done,
                    onSubmitted: (_) {
                      if (!_totpRequired) _submit();
                    },
                  ),
                  if (_totpRequired) ...[
                    const SizedBox(height: 16),
                    Text(
                      'This account has 2FA enabled. Enter the code from your authenticator app.',
                      style: Theme.of(context).textTheme.bodySmall,
                    ),
                    const SizedBox(height: 8),
                    TextField(
                      controller: _totpController,
                      autofocus: true,
                      keyboardType: TextInputType.number,
                      maxLength: 6,
                      decoration: const InputDecoration(
                        labelText: '6-digit code',
                        border: OutlineInputBorder(),
                      ),
                      onSubmitted: (_) => _submit(),
                    ),
                  ],
                  if (_errorMessage != null) ...[
                    const SizedBox(height: 8),
                    Text(
                      _errorMessage!,
                      style: TextStyle(color: Theme.of(context).colorScheme.error),
                    ),
                  ],
                  const SizedBox(height: 24),
                  FilledButton(
                    onPressed: authState.isLoading ? null : _submit,
                    child: authState.isLoading
                        ? const SizedBox(
                            height: 20,
                            width: 20,
                            child: CircularProgressIndicator(strokeWidth: 2),
                          )
                        : Text(_totpRequired ? 'Verify' : 'Log in'),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}