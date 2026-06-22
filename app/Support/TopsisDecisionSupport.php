<?php

namespace App\Support;

use App\Models\TravelPackage;
use Illuminate\Support\Collection;
use InvalidArgumentException;

class TopsisDecisionSupport
{
    public const CRITERIA = [
        'budgetFit' => 'Budget fit',
        'destinationMatch' => 'Destination match',
        'availableSlots' => 'Available slots',
        'supplierReliability' => 'Supplier reliability',
        'durationFit' => 'Duration fit',
        'travelTypeMatch' => 'Travel type match',
        'businessValue' => 'Business value',
    ];

    public const DEFAULT_WEIGHTS = [
        'budgetFit' => 0.20,
        'destinationMatch' => 0.20,
        'availableSlots' => 0.15,
        'supplierReliability' => 0.15,
        'durationFit' => 0.10,
        'travelTypeMatch' => 0.10,
        'businessValue' => 0.10,
    ];

    public static function rankPackages(Collection $packages, array $preferences = [], array $weights = self::DEFAULT_WEIGHTS): array
    {
        self::assertValidWeights($weights);

        $maxSlots = max((int) $packages->max('available_slots'), 1);
        $alternatives = $packages
            ->map(fn (TravelPackage $package) => self::alternative($package, $preferences, $maxSlots))
            ->values();

        if ($alternatives->isEmpty()) {
            return [
                'preferences' => self::preferences($preferences),
                'weights' => self::weightRows($weights),
                'results' => [],
                'best' => null,
                'summary' => 'No package options are available for the decision guide yet.',
                'warning' => 'Add package presets before comparing options.',
            ];
        }

        $weighted = self::weightedMatrix($alternatives, $weights);
        $idealBest = [];
        $idealWorst = [];

        foreach (array_keys(self::CRITERIA) as $criterion) {
            $column = array_column($weighted, $criterion);
            $idealBest[$criterion] = max($column);
            $idealWorst[$criterion] = min($column);
        }

        $results = $alternatives
            ->map(function (array $alternative, int $index) use ($weighted, $idealBest, $idealWorst): array {
                $distanceBest = 0.0;
                $distanceWorst = 0.0;

                foreach (array_keys(self::CRITERIA) as $criterion) {
                    $distanceBest += ($weighted[$index][$criterion] - $idealBest[$criterion]) ** 2;
                    $distanceWorst += ($weighted[$index][$criterion] - $idealWorst[$criterion]) ** 2;
                }

                $distanceBest = sqrt($distanceBest);
                $distanceWorst = sqrt($distanceWorst);
                $score = ($distanceBest + $distanceWorst) > 0
                    ? $distanceWorst / ($distanceBest + $distanceWorst)
                    : 0.5;

                return [
                    ...$alternative,
                    'score' => round($score, 4),
                    'scorePercent' => round($score * 100, 1),
                    'distanceToIdeal' => round($distanceBest, 4),
                    'distanceFromWorst' => round($distanceWorst, 4),
                    'explanation' => self::explanation($alternative),
                    'weakness' => self::weakness($alternative),
                ];
            })
            ->sortBy([
                ['score', 'desc'],
                ['packageName', 'asc'],
            ])
            ->values()
            ->map(function (array $result, int $index): array {
                return [
                    ...$result,
                    'rank' => $index + 1,
                ];
            })
            ->all();

        $best = $results[0] ?? null;

        return [
            'preferences' => self::preferences($preferences),
            'weights' => self::weightRows($weights),
            'results' => $results,
            'best' => $best,
            'summary' => $best
                ? $best['packageName'].' is closest to the ideal package profile for the selected priorities.'
                : 'No package comparison was produced.',
            'warning' => $best && $best['score'] < 0.45
                ? 'No option is a strong match. Review criteria or add more package alternatives.'
                : null,
        ];
    }

    public static function assertValidWeights(array $weights): void
    {
        $expected = array_keys(self::CRITERIA);
        $missing = array_diff($expected, array_keys($weights));
        $extra = array_diff(array_keys($weights), $expected);

        if ($missing || $extra) {
            throw new InvalidArgumentException('TOPSIS weights must match the configured criteria.');
        }

        foreach ($weights as $weight) {
            if (! is_numeric($weight) || $weight < 0) {
                throw new InvalidArgumentException('TOPSIS weights must be non-negative numbers.');
            }
        }

        $sum = array_sum($weights);

        if (abs($sum - 1.0) > 0.001) {
            throw new InvalidArgumentException('TOPSIS weights must total 1.00.');
        }
    }

    private static function alternative(TravelPackage $package, array $preferences, int $maxSlots): array
    {
        $targetBudget = (int) ($preferences['budget'] ?? 12000);
        $targetDestination = trim((string) ($preferences['destination'] ?? ''));
        $targetDuration = self::durationDays((string) ($preferences['duration'] ?? '3D2N'));
        $targetTravelType = trim((string) ($preferences['travelType'] ?? ''));
        $durationDays = self::durationDays((string) $package->duration);
        $scores = [
            'budgetFit' => self::budgetFit((int) $package->base_price, $targetBudget),
            'destinationMatch' => self::destinationMatch((string) $package->destination, $targetDestination),
            'availableSlots' => self::scale((int) $package->available_slots, $maxSlots),
            'supplierReliability' => self::clamp((int) $package->supplier_reliability_score),
            'durationFit' => self::durationFit($durationDays, $targetDuration),
            'travelTypeMatch' => self::travelTypeMatch((string) $package->travel_type, $targetTravelType),
            'businessValue' => self::clamp((int) $package->business_value_score),
        ];

        return [
            'id' => $package->code,
            'packageName' => $package->package_name,
            'destination' => $package->destination,
            'duration' => $package->duration,
            'basePrice' => (int) $package->base_price,
            'travelType' => $package->travel_type,
            'availableSlots' => (int) $package->available_slots,
            'status' => $package->status,
            'supplierReliabilityScore' => (int) $package->supplier_reliability_score,
            'businessValueScore' => (int) $package->business_value_score,
            'riskScore' => (int) $package->risk_score,
            'criteria' => $scores,
            'criteriaSummary' => self::criteriaSummary($scores),
        ];
    }

    private static function weightedMatrix(Collection $alternatives, array $weights): array
    {
        $norms = [];

        foreach (array_keys(self::CRITERIA) as $criterion) {
            $sumSquares = $alternatives->sum(fn (array $alternative) => $alternative['criteria'][$criterion] ** 2);
            $norms[$criterion] = sqrt($sumSquares) ?: 1;
        }

        return $alternatives
            ->map(function (array $alternative) use ($norms, $weights): array {
                $row = [];

                foreach (array_keys(self::CRITERIA) as $criterion) {
                    $row[$criterion] = ($alternative['criteria'][$criterion] / $norms[$criterion]) * $weights[$criterion];
                }

                return $row;
            })
            ->all();
    }

    private static function budgetFit(int $basePrice, int $targetBudget): float
    {
        if ($targetBudget <= 0) {
            return 80;
        }

        $gapRatio = abs($basePrice - $targetBudget) / $targetBudget;

        return self::clamp(100 - ($gapRatio * 100));
    }

    private static function destinationMatch(string $destination, string $targetDestination): float
    {
        if ($targetDestination === '') {
            return 80;
        }

        $destination = strtolower($destination);
        $targetDestination = strtolower($targetDestination);

        if ($destination === $targetDestination) {
            return 100;
        }

        return str_contains($destination, $targetDestination) || str_contains($targetDestination, $destination)
            ? 75
            : 35;
    }

    private static function durationFit(int $durationDays, int $targetDuration): float
    {
        if ($targetDuration <= 0 || $durationDays <= 0) {
            return 80;
        }

        return self::clamp(100 - (abs($durationDays - $targetDuration) * 25));
    }

    private static function travelTypeMatch(string $travelType, string $targetTravelType): float
    {
        if ($targetTravelType === '') {
            return 80;
        }

        return strtolower($travelType) === strtolower($targetTravelType) ? 100 : 55;
    }

    private static function durationDays(string $duration): int
    {
        if (preg_match('/(\d+)\s*d/i', $duration, $matches)) {
            return (int) $matches[1];
        }

        return 0;
    }

    private static function scale(int $value, int $max): float
    {
        return self::clamp(($value / max($max, 1)) * 100);
    }

    private static function clamp(float $value): float
    {
        return round(min(max($value, 0), 100), 2);
    }

    private static function criteriaSummary(array $scores): array
    {
        return collect($scores)
            ->map(fn (float $score, string $key) => [
                'key' => $key,
                'label' => self::CRITERIA[$key],
                'score' => round($score, 1),
            ])
            ->values()
            ->all();
    }

    private static function explanation(array $alternative): string
    {
        $strongest = collect($alternative['criteriaSummary'])
            ->sortByDesc('score')
            ->take(3)
            ->pluck('label')
            ->map(fn (string $label) => strtolower($label))
            ->join(', ');

        return $alternative['packageName'].' is a strong fit because of '.$strongest.'.';
    }

    private static function weakness(array $alternative): string
    {
        $weakest = collect($alternative['criteriaSummary'])
            ->sortBy('score')
            ->first();

        return $weakest
            ? 'Lowest criterion: '.$weakest['label'].' ('.$weakest['score'].'/100).'
            : 'No weak criterion detected.';
    }

    private static function preferences(array $preferences): array
    {
        return [
            'budget' => (int) ($preferences['budget'] ?? 12000),
            'destination' => (string) ($preferences['destination'] ?? ''),
            'duration' => (string) ($preferences['duration'] ?? '3D2N'),
            'travelType' => (string) ($preferences['travelType'] ?? ''),
        ];
    }

    private static function weightRows(array $weights): array
    {
        return collect($weights)
            ->map(fn (float $weight, string $key) => [
                'key' => $key,
                'label' => self::CRITERIA[$key],
                'weight' => $weight,
                'percent' => round($weight * 100),
            ])
            ->values()
            ->all();
    }
}
