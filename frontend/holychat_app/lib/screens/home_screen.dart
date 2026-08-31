import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:matrix/matrix.dart';
import '../app_config.dart';
import '../providers/auth_provider.dart';
import '../providers/matrix_provider.dart';
import '../providers/rooms_provider.dart';
import 'chat_screen.dart';

/// Lista de conversas - o ecrã principal depois do login.
class HomeScreen extends ConsumerWidget {
  const HomeScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final authState = ref.watch(authProvider);
    final user = authState.user!;
    final roomsAsync = ref.watch(roomsProvider);

    return Scaffold(
      appBar: AppBar(
        title: Text(user.displayName),
        actions: [
          IconButton(
            icon: const Icon(Icons.logout),
            onPressed: () => ref.read(authProvider.notifier).logout(),
          ),
        ],
      ),
      floatingActionButton: FloatingActionButton(
        onPressed: () => _showNewChatDialog(context, ref),
        child: const Icon(Icons.add),
      ),
      body: roomsAsync.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (err, _) => Center(child: Text('Failed to load conversations: $err')),
        data: (rooms) {
          if (rooms.isEmpty) {
            return const Center(
              child: Padding(
                padding: EdgeInsets.all(24),
                child: Text(
                  'No conversations yet.',
                  textAlign: TextAlign.center,
                ),
              ),
            );
          }

          // Ordena pela mais recentemente ativa primeiro.
          final sorted = [...rooms]..sort((a, b) =>
              (b.lastEvent?.originServerTs ?? DateTime.fromMillisecondsSinceEpoch(0))
                  .compareTo(a.lastEvent?.originServerTs ?? DateTime.fromMillisecondsSinceEpoch(0)));

          return ListView.builder(
            itemCount: sorted.length,
            itemBuilder: (context, index) {
              final room = sorted[index];
              return _RoomTile(room: room);
            },
          );
        },
      ),
    );
  }
}

class _RoomTile extends StatelessWidget {
  final Room room;
  const _RoomTile({required this.room});

  @override
  Widget build(BuildContext context) {
    final lastEvent = room.lastEvent;
    final preview = lastEvent?.body ?? '';

    return ListTile(
      leading: CircleAvatar(
        child: Text(room.displayname.isNotEmpty ? room.displayname[0].toUpperCase() : '?'),
      ),
      title: Text(room.displayname),
      subtitle: Text(
        preview,
        maxLines: 1,
        overflow: TextOverflow.ellipsis,
      ),
      onTap: () {
        Navigator.of(context).push(
          MaterialPageRoute(builder: (_) => ChatScreen(room: room)),
        );
      },
    );
  }
}

Future<void> _showNewChatDialog(BuildContext context, WidgetRef ref) async {
  final controller = TextEditingController();

  final username = await showDialog<String>(
    context: context,
    builder: (context) => AlertDialog(
      title: const Text('New conversation'),
      content: TextField(
        controller: controller,
        decoration: const InputDecoration(
          labelText: 'Username',
          hintText: 'e.g. novoamigo',
        ),
        autofocus: true,
        onSubmitted: (value) => Navigator.of(context).pop(value.trim()),
      ),
      actions: [
        TextButton(
          onPressed: () => Navigator.of(context).pop(),
          child: const Text('Cancel'),
        ),
        FilledButton(
          onPressed: () => Navigator.of(context).pop(controller.text.trim()),
          child: const Text('Start chat'),
        ),
      ],
    ),
  );

  if (username == null || username.isEmpty || !context.mounted) return;

  final matrix = ref.read(matrixServiceProvider);
  // ServerName vem do AppConfig - assume-se o mesmo formato usado
  // no resto da app (@username:holychat.cc).
  final serverName = Uri.parse(AppConfig.matrixHomeserverUrl).host.replaceFirst('matrix.', '');
  final userId = '@$username:$serverName';

  try {
    final roomId = await matrix.client.startDirectChat(userId);
    final room = matrix.client.getRoomById(roomId);
    if (room != null && context.mounted) {
      Navigator.of(context).push(
        MaterialPageRoute(builder: (_) => ChatScreen(room: room)),
      );
    }
  } catch (e) {
    if (context.mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('Could not start chat: $e')),
      );
    }
  }
}
