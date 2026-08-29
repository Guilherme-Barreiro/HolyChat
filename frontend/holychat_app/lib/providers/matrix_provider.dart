import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../services/matrix_service.dart';

final matrixServiceProvider = Provider<MatrixService>((ref) => MatrixService());
