import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:holychat_app/main.dart';

void main() {
  testWidgets('HolyChat login screen renders', (tester) async {
    await tester.pumpWidget(const ProviderScope(child: HolyChatApp()));
    expect(find.text('HolyChat'), findsOneWidget);
    expect(find.byType(TextField), findsWidgets);
  });
}
