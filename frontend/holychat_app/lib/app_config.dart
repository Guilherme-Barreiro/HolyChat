/// URL base do backend HolyChat.
///
/// IMPORTANTE - dependendo de onde estás a testar, "localhost" não
/// significa a mesma coisa:
/// - Emulador Android: usa 10.0.2.2 (é como o emulador vê o teu PC)
/// - Dispositivo físico (USB ou WiFi): usa o IP da tua máquina na
///   rede local (ex: 192.168.1.X), e confirma que a firewall do
///   Windows permite ligações de entrada na porta do backend
/// - Chrome/Web: localhost funciona normalmente
///
/// Muda esta constante consoante o dispositivo em que estás a testar.
/// Mais tarde, quando o backend estiver publicado (ex: api.holychat.cc
/// via Cloudflare Tunnel), troca para essa URL de produção.
class AppConfig {
  //static const String apiBaseUrl = 'http://10.0.2.2:5175';
  static const String apiBaseUrl = 'http://localhost:5175';
}
