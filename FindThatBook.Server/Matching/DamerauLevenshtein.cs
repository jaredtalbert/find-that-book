namespace FindThatBook.Server.Matching;

static class DamerauLevenshtein {
    public static int Distance(string left, string right) {
        if (left.Length == 0) {
            return right.Length;
        }

        if (right.Length == 0) {
            return left.Length;
        }

        int[,] distances = new int[left.Length + 1, right.Length + 1];

        for (int leftIndex = 0; leftIndex <= left.Length; leftIndex++) {
            distances[leftIndex, 0] = leftIndex;
        }

        for (int rightIndex = 0; rightIndex <= right.Length; rightIndex++) {
            distances[0, rightIndex] = rightIndex;
        }

        for (int leftIndex = 1; leftIndex <= left.Length; leftIndex++) {
            for (int rightIndex = 1; rightIndex <= right.Length; rightIndex++) {
                int substitutionCost = left[leftIndex - 1] == right[rightIndex - 1] ? 0 : 1;

                int distance = Math.Min(
                    distances[leftIndex - 1, rightIndex] + 1,
                    Math.Min(
                        distances[leftIndex, rightIndex - 1] + 1,
                        distances[leftIndex - 1, rightIndex - 1] + substitutionCost));

                if (leftIndex > 1 &&
                    rightIndex > 1 &&
                    left[leftIndex - 1] == right[rightIndex - 2] &&
                    left[leftIndex - 2] == right[rightIndex - 1]) {
                    distance = Math.Min(distance, distances[leftIndex - 2, rightIndex - 2] + 1);
                }

                distances[leftIndex, rightIndex] = distance;
            }
        }

        return distances[left.Length, right.Length];
    }
}