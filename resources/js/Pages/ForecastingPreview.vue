<template>
    <AppShell
        active-label="Package Decision Guide"
        eyebrow="Owner Decision Support"
        title="Package Decision Guide"
        description="Compare package options by budget fit, capacity, supplier reliability, risk, and business value."
    >
        <section class="dss-page">
            <DecisionSignal
                badge="Suggested package"
                tone="primary"
                icon="shieldCheck"
                :meta="[`${results.length} options compared`, 'TOPSIS method']"
                :title="best ? `${best.packageName} is the best fit to review` : 'Add package presets to compare options'"
                :description="summary"
                :chips="recommendationChips"
                action-label="Open Explanation"
                @action="selectedResult = best"
            />

            <ActionNotice
                v-if="warning"
                tone="warning"
                title="Review the package options"
                :message="warning"
            />

            <section class="stat-grid dss-kpi-grid">
                <StatCard
                    v-for="stat in rankingStats"
                    :key="stat.label"
                    :icon="stat.icon"
                    :label="stat.label"
                    :value="stat.value"
                    :note="stat.note"
                    :status="stat.status"
                    :tone="stat.tone"
                />
            </section>

            <section class="topsis-workspace-grid">
                <ContentPanel icon="filter" eyebrow="Owner Priorities" title="Decision Priorities" badge="Editable">
                    <form class="topsis-form" @submit.prevent="submitRanking">
                        <div class="form-grid">
                            <label class="account-field">
                                <span>Target budget</span>
                                <input v-model.number="form.budget" type="number" min="0" max="500000" />
                            </label>
                            <label class="account-field">
                                <span>Destination</span>
                                <input v-model.trim="form.destination" maxlength="120" placeholder="Any destination" />
                            </label>
                            <label class="account-field">
                                <span>Duration</span>
                                <select v-model="form.duration">
                                    <option value="2D1N">2D1N</option>
                                    <option value="3D2N">3D2N</option>
                                    <option value="4D3N">4D3N</option>
                                </select>
                            </label>
                            <label class="account-field">
                                <span>Travel type</span>
                                <select v-model="form.travelType">
                                    <option value="">Any type</option>
                                    <option>Leisure</option>
                                    <option>Educational</option>
                                    <option>Cultural</option>
                                    <option>Adventure</option>
                                    <option>Corporate</option>
                                </select>
                            </label>
                        </div>

                        <div class="topsis-action-row">
                            <button class="primary-button compact-button" type="submit">
                                <AppIcon name="shieldCheck" />
                                Compare Packages
                            </button>
                            <button class="secondary-button compact-button" type="button" @click="resetRanking">
                                Restore Defaults
                            </button>
                        </div>
                    </form>
                </ContentPanel>

                <ContentPanel icon="fileBarChart" eyebrow="Method" title="How The Guide Scores Packages" badge="TOPSIS">
                    <div class="topsis-weight-list">
                        <div v-for="weight in weights" :key="weight.key">
                            <span>{{ weight.label }}</span>
                            <strong>{{ weight.percent }}%</strong>
                            <meter min="0" max="100" :value="weight.percent">{{ weight.percent }}%</meter>
                        </div>
                    </div>
                </ContentPanel>
            </section>

            <ContentPanel icon="shieldCheck" eyebrow="Decision Output" title="Package Comparison Results" badge="Decision support">
                <DataTableFrame label="Package decision comparison table">
                    <table class="dss-table topsis-table">
                        <thead>
                            <tr>
                                <th>Rank</th>
                                <th>Package</th>
                                <th>Fit Score</th>
                                <th>Strongest Fit</th>
                                <th>Needs Review</th>
                                <th>Open</th>
                            </tr>
                        </thead>
                        <tbody>
                            <tr v-for="result in results" :key="result.id" :class="{ selected: selectedResult?.id === result.id }">
                                <td><strong>#{{ result.rank }}</strong></td>
                                <td>
                                    <strong>{{ result.packageName }}</strong>
                                    <span>{{ result.destination }} / {{ result.duration }} / {{ formatCurrency(result.basePrice) }}</span>
                                </td>
                                <td>
                                    <span class="topsis-score">{{ result.scorePercent }}%</span>
                                    <span>{{ result.travelType }}</span>
                                </td>
                                <td>{{ topCriteria(result).join(', ') }}</td>
                                <td>{{ result.weakness }}</td>
                                <td>
                                    <button class="secondary-button compact-button" type="button" @click="selectedResult = result">
                                        <AppIcon name="search" />
                                        Details
                                    </button>
                                </td>
                            </tr>
                        </tbody>
                    </table>
                </DataTableFrame>
            </ContentPanel>

            <AppDrawer
                v-if="selectedResult"
                eyebrow="Decision Explanation"
                :title="selectedResult.packageName"
                @close="selectedResult = null"
            >
                <DecisionSignal
                    badge="Why this package"
                    tone="primary"
                    icon="shieldCheck"
                    :meta="[`Rank #${selectedResult.rank}`, `${selectedResult.scorePercent}% fit score`, 'TOPSIS method']"
                    :title="selectedResult.explanation"
                    :description="selectedResult.weakness"
                    :chips="[selectedResult.destination, selectedResult.duration, selectedResult.travelType]"
                    compact
                    embedded
                    heading-tag="h3"
                />

                <div class="topsis-criteria-list">
                    <div v-for="criterion in selectedResult.criteriaSummary" :key="criterion.key">
                        <span>{{ criterion.label }}</span>
                        <strong>{{ criterion.score }}/100</strong>
                        <meter min="0" max="100" :value="criterion.score">{{ criterion.score }}/100</meter>
                    </div>
                </div>
            </AppDrawer>
        </section>
    </AppShell>
</template>

<script>
import { router } from '@inertiajs/vue3';
import ActionNotice from '../Components/feedback/ActionNotice.vue';
import AppDrawer from '../Components/feedback/AppDrawer.vue';
import AppIcon from '../Components/icons/AppIcon.vue';
import AppShell from '../Components/layout/AppShell.vue';
import ContentPanel from '../Components/dashboard/ContentPanel.vue';
import DataTableFrame from '../Components/records/DataTableFrame.vue';
import DecisionSignal from '../Components/dss/DecisionSignal.vue';
import StatCard from '../Components/dashboard/StatCard.vue';
import { formatCurrency } from '../utils/formatters';

export default {
    name: 'ForecastingPreview',
    components: {
        ActionNotice,
        AppDrawer,
        AppIcon,
        AppShell,
        ContentPanel,
        DataTableFrame,
        DecisionSignal,
        StatCard,
    },
    props: {
        topsis: {
            type: Object,
            default: () => ({
                preferences: {},
                weights: [],
                results: [],
                best: null,
                summary: '',
                warning: null,
            }),
        },
    },
    data() {
        return {
            selectedResult: null,
            form: {
                budget: this.topsis.preferences?.budget || 12000,
                destination: this.topsis.preferences?.destination || '',
                duration: this.topsis.preferences?.duration || '3D2N',
                travelType: this.topsis.preferences?.travelType || '',
            },
        };
    },
    computed: {
        results() {
            return this.topsis.results || [];
        },
        best() {
            return this.topsis.best || null;
        },
        weights() {
            return this.topsis.weights || [];
        },
        summary() {
            return this.topsis.summary || 'The guide compares available packages against the selected priorities.';
        },
        warning() {
            return this.topsis.warning;
        },
        recommendationChips() {
            if (!this.best) {
                return ['No alternatives', 'Add package data'];
            }

            return [
                `${this.best.scorePercent}% fit score`,
                this.best.destination,
                `${this.best.availableSlots} slots`,
            ];
        },
        rankingStats() {
            return [
                { icon: 'boxes', label: 'Options', value: String(this.results.length), note: 'Package presets compared', status: 'Ready', tone: 'primary' },
                { icon: 'shieldCheck', label: 'Best Fit', value: this.best ? `${this.best.scorePercent}%` : '0%', note: this.best?.packageName || 'No package selected', status: 'Review', tone: 'success' },
                { icon: 'wallet', label: 'Budget Target', value: formatCurrency(this.form.budget), note: 'Used for budget-fit scoring', status: 'Criteria', tone: 'data' },
            ];
        },
    },
    methods: {
        formatCurrency,
        submitRanking() {
            router.get('/decision-guide', this.form, {
                preserveScroll: true,
                preserveState: false,
            });
        },
        resetRanking() {
            this.form = {
                budget: 12000,
                destination: '',
                duration: '3D2N',
                travelType: '',
            };
            router.get('/decision-guide', this.form, {
                preserveScroll: true,
                preserveState: false,
            });
        },
        topCriteria(result) {
            return [...(result.criteriaSummary || [])]
                .sort((a, b) => b.score - a.score)
                .slice(0, 2)
                .map((criterion) => criterion.label);
        },
    },
};
</script>
