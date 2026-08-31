import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:matrix/matrix.dart';
import 'matrix_provider.dart';

/// Emite a lista de salas sempre que o cliente Matrix sincroniza
/// (mensagens novas, salas novas, etc.) - dá uma UI que se atualiza
/// sozinha sem precisares de refresh manual.
final roomsProvider = StreamProvider<List<Room>>((ref) async* {
  final matrix = ref.watch(matrixServiceProvider);
  yield matrix.client.rooms;
  await for (final _ in matrix.client.onSync.stream) {
    yield matrix.client.rooms;
  }
});
