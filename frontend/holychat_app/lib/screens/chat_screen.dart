import 'package:flutter/material.dart';
import 'package:matrix/matrix.dart';

class ChatScreen extends StatefulWidget {
  final Room room;
  const ChatScreen({super.key, required this.room});

  @override
  State<ChatScreen> createState() => _ChatScreenState();
}

class _ChatScreenState extends State<ChatScreen> {
  final _messageController = TextEditingController();
  Timeline? _timeline;
  bool _loading = true;
  bool _sending = false;

  @override
  void initState() {
    super.initState();
    _loadTimeline();
  }

  Future<void> _loadTimeline() async {
    // Se ainda só temos um convite pendente para esta sala (alguém
    // começou um chat connosco), aceita automaticamente antes de
    // carregar mensagens - faz sentido num grupo fechado de amigos
    // de confiança, sem precisar de um ecrã extra de aceitar/recusar.
    if (widget.room.membership == Membership.invite) {
      try {
        await widget.room.join();
      } catch (e) {
        if (mounted) {
          setState(() => _loading = false);
          ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(content: Text('Failed to join conversation: $e')),
          );
        }
        return;
      }
    }

    // onUpdate é chamado sempre que uma mensagem nova chega ou o
    // estado da timeline muda - basta fazer setState para a UI
    // refletir isso automaticamente.
    final timeline = await widget.room.getTimeline(
      onUpdate: () {
        if (mounted) setState(() {});
      },
    );
    if (mounted) {
      setState(() {
        _timeline = timeline;
        _loading = false;
      });
    }
  }

  @override
  void dispose() {
    _messageController.dispose();
    super.dispose();
  }

  Future<void> _sendMessage() async {
    final text = _messageController.text.trim();
    if (text.isEmpty || _sending) return;

    setState(() => _sending = true);
    _messageController.clear();
    try {
      await widget.room.sendTextEvent(text);
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Failed to send message: $e')),
        );
      }
    } finally {
      if (mounted) setState(() => _sending = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: Text(widget.room.displayname)),
      body: Column(
        children: [
          Expanded(
            child: _loading
                ? const Center(child: CircularProgressIndicator())
                : _buildMessageList(),
          ),
          _buildComposer(),
        ],
      ),
    );
  }

  Widget _buildMessageList() {
    final events = _timeline?.events ?? [];
    // Mensagens só de texto visíveis por agora - eventos de estado
    // (entradas/saídas de sala, etc.) são filtrados.
    final messageEvents = events.where((e) => e.type == EventTypes.Message).toList();

    if (messageEvents.isEmpty) {
      return const Center(child: Text('No messages yet. Say hi!'));
    }

    // O SDK devolve a timeline com o evento mais recente no índice 0,
    // e um ListView invertido mostra-os em baixo, como é habitual
    // em apps de chat.
    return ListView.builder(
      reverse: true,
      padding: const EdgeInsets.all(12),
      itemCount: messageEvents.length,
      itemBuilder: (context, index) {
        final event = messageEvents[index];
        final isMe = event.senderId == widget.room.client.userID;
        return _MessageBubble(event: event, isMe: isMe);
      },
    );
  }

  Widget _buildComposer() {
    return SafeArea(
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 8),
        child: Row(
          children: [
            Expanded(
              child: TextField(
                controller: _messageController,
                decoration: const InputDecoration(
                  hintText: 'Message',
                  border: OutlineInputBorder(),
                  contentPadding: EdgeInsets.symmetric(horizontal: 12, vertical: 8),
                ),
                minLines: 1,
                maxLines: 4,
                textInputAction: TextInputAction.send,
                onSubmitted: (_) => _sendMessage(),
              ),
            ),
            const SizedBox(width: 8),
            IconButton.filled(
              onPressed: _sending ? null : _sendMessage,
              icon: _sending
                  ? const SizedBox(
                      width: 18,
                      height: 18,
                      child: CircularProgressIndicator(strokeWidth: 2),
                    )
                  : const Icon(Icons.send),
            ),
          ],
        ),
      ),
    );
  }
}

class _MessageBubble extends StatelessWidget {
  final Event event;
  final bool isMe;
  const _MessageBubble({required this.event, required this.isMe});

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Align(
      alignment: isMe ? Alignment.centerRight : Alignment.centerLeft,
      child: Container(
        margin: const EdgeInsets.symmetric(vertical: 4),
        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
        constraints: BoxConstraints(maxWidth: MediaQuery.of(context).size.width * 0.75),
        decoration: BoxDecoration(
          color: isMe ? colorScheme.primary : colorScheme.surfaceContainerHighest,
          borderRadius: BorderRadius.circular(12),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          mainAxisSize: MainAxisSize.min,
          children: [
            if (!isMe)
              Text(
                event.senderFromMemoryOrFallback.calcDisplayname(),
                style: TextStyle(
                  fontSize: 12,
                  fontWeight: FontWeight.bold,
                  color: colorScheme.onSurfaceVariant,
                ),
              ),
            Text(
              event.body,
              style: TextStyle(
                color: isMe ? colorScheme.onPrimary : colorScheme.onSurfaceVariant,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

