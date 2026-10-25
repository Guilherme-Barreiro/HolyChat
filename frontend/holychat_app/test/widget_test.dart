import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:holychat_app/main.dart';

void main() {
  testWidgets('HolyChat application displays the login screen', (tester) async {
    await tester.pumpWidget(const HolyChatApp());
    expect(find.text('HolyChat'), findsOneWidget);
    expect(find.byType(TextField), findsWidgets);
  });
}
