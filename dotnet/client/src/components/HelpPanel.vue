<template>
  <Drawer :open="state.open" title="Help" @close="closeHelp">
    <div class="help">
      <SearchField v-model="query" placeholder="Search help" class="help-search" />

      <article v-if="current" class="help-topic">
        <button type="button" class="help-back" @click="showList">
          <UiIcon name="arrow-right" class="help-back-icon" />
          All help
        </button>
        <h3 ref="topicHeading" class="help-topic-title" tabindex="-1">{{ current.title }}</h3>
        <p class="help-topic-summary">{{ current.summary }}</p>

        <template v-for="(block, index) in current.blocks" :key="index">
          <p v-if="block.kind === 'text'" class="help-text">{{ block.text }}</p>
          <ol v-else-if="block.kind === 'steps'" class="help-steps">
            <li v-for="item in block.items" :key="item">{{ item }}</li>
          </ol>
          <ul v-else-if="block.kind === 'list'" class="help-list">
            <li v-for="item in block.items" :key="item">{{ item }}</li>
          </ul>
          <dl v-else-if="block.kind === 'terms'" class="help-terms">
            <div v-for="[term, meaning] in block.items" :key="term">
              <dt>{{ term }}</dt>
              <dd>{{ meaning }}</dd>
            </div>
          </dl>
          <dl v-else-if="block.kind === 'keys'" class="help-keys">
            <div v-for="[keys, meaning] in block.items" :key="meaning">
              <dt>
                <template v-for="(key, keyIndex) in keys" :key="keyIndex">
                  <span v-if="key === 'then'" class="help-then">then</span>
                  <kbd v-else>{{ displayKey(key) }}</kbd>
                </template>
              </dt>
              <dd>{{ meaning }}</dd>
            </div>
          </dl>
        </template>

        <div v-if="related.length" class="help-related">
          <p class="help-related-title">Related</p>
          <button v-for="topic in related" :key="topic.id" type="button" class="help-related-link" @click="openTopic(topic.id)">
            {{ topic.title }}
            <UiIcon name="arrow-right" />
          </button>
        </div>
      </article>

      <template v-else>
        <section v-if="!searching && onThisPage.length" class="help-group" aria-labelledby="help-here">
          <h3 id="help-here" class="help-group-title">On this page</h3>
          <ul class="help-topics">
            <li v-for="topic in onThisPage" :key="topic.id">
              <button type="button" class="help-topic-link" @click="openTopic(topic.id)">
                <span class="help-topic-link-title">{{ topic.title }}</span>
                <span class="help-topic-link-summary">{{ topic.summary }}</span>
              </button>
            </li>
          </ul>
        </section>

        <section class="help-group" aria-labelledby="help-all">
          <h3 id="help-all" class="help-group-title" aria-live="polite">
            {{ searching ? (results.length === 1 ? '1 topic' : `${results.length} topics`) : onThisPage.length ? 'Everything else' : 'All help' }}
          </h3>
          <ul v-if="listed.length" class="help-topics">
            <li v-for="topic in listed" :key="topic.id">
              <button type="button" class="help-topic-link" @click="openTopic(topic.id)">
                <span class="help-topic-link-title">{{ topic.title }}</span>
                <span class="help-topic-link-summary">{{ topic.summary }}</span>
              </button>
            </li>
          </ul>
          <p v-else class="help-empty">
            Nothing matches “{{ query.trim() }}”. Try a plainer word, such as booking, paid, forecast or export.
          </p>
        </section>
      </template>
    </div>
  </Drawer>
</template>

<script setup lang="ts">
import { computed, nextTick, ref, watch } from 'vue';
import { useRoute } from 'vue-router';
import Drawer from './Drawer.vue';
import SearchField from './SearchField.vue';
import UiIcon from './UiIcon.vue';
import { closeHelp, useHelpState } from '../composables/useHelp';
import { useAuth } from '../stores/auth';
import { canAccess } from '../nav';
import { helpTopics, topicById, topicText, type HelpTopic } from '../help';
import { modKey } from '../keyboard';

const state = useHelpState();
const route = useRoute();
const { state: auth } = useAuth();

const query = ref('');
const topicId = ref('');
const topicHeading = ref<HTMLElement | null>(null);

// Help covers only what this person can open: Staff are not shown the forecast or the team.
const available = computed(() =>
  helpTopics.filter((topic) => !topic.access || canAccess(auth.user?.role, topic.access)),
);

const current = computed(() => available.value.find((topic) => topic.id === topicId.value) ?? null);
const related = computed(() =>
  (current.value?.related ?? [])
    .map((id) => topicById(id))
    .filter((topic): topic is HelpTopic => !!topic && available.value.includes(topic)),
);

const searching = computed(() => query.value.trim().length > 0);
// The few topics that belong to this page first: those whose own page it is, then the rest that
// touch it. Four at most, so the list can be taken in at a glance.
const onThisPage = computed(() => {
  const here = route.path;
  const own = available.value.filter((topic) => topic.pages[0] === here);
  const touching = available.value.filter((topic) => topic.pages[0] !== here && topic.pages.includes(here));
  return [...own, ...touching].slice(0, 4);
});

const results = computed(() => {
  const words = query.value.trim().toLowerCase().split(/\s+/).filter(Boolean);
  if (!words.length) return [];
  return available.value
    .map((topic) => {
      const text = topicText(topic);
      if (!words.every((word) => text.includes(word))) return null;
      const title = topic.title.toLowerCase();
      const score = words.reduce((sum, word) => sum + (title.includes(word) ? 3 : 0) + (topic.keywords.includes(word) ? 1 : 0), 0);
      return { topic, score };
    })
    .filter((hit): hit is { topic: HelpTopic; score: number } => hit !== null)
    .sort((a, b) => b.score - a.score)
    .map((hit) => hit.topic);
});

const listed = computed(() => {
  if (searching.value) return results.value;
  const here = new Set(onThisPage.value.map((topic) => topic.id));
  return available.value.filter((topic) => !here.has(topic.id));
});

function displayKey(key: string): string {
  return key === 'Ctrl' ? modKey : key;
}

async function openTopic(id: string) {
  topicId.value = id;
  await nextTick();
  topicHeading.value?.focus();
  topicHeading.value?.closest('.slide-drawer-body')?.scrollTo({ top: 0 });
}

function showList() {
  topicId.value = '';
}

// Opening help goes straight to the topic asked for, or to the list for this page.
watch(
  () => [state.open, state.topic] as const,
  ([open, topic]) => {
    if (!open) return;
    query.value = '';
    topicId.value = topic && available.value.some((t) => t.id === topic) ? topic : '';
    if (topicId.value) void openTopic(topicId.value);
  },
);

// Typing a search leaves the topic being read.
watch(query, (value) => {
  if (value.trim()) topicId.value = '';
});
</script>

<style scoped>
.help {
  display: grid;
  gap: 20px;
}

.help-search {
  width: 100%;
}

.help-group {
  display: grid;
  gap: 8px;
}

.help-group-title {
  color: var(--color-text-muted);
  font-size: var(--text-sm);
  font-weight: 600;
}

.help-topics {
  display: grid;
  margin: 0 -12px;
  padding: 0;
  list-style: none;
}

.help-topic-link {
  display: grid;
  gap: 2px;
  width: 100%;
  padding: 10px 12px;
  border: 0;
  border-radius: var(--radius-md);
  background: none;
  color: inherit;
  font: inherit;
  text-align: left;
  cursor: pointer;
  transition: background var(--transition-fast);
}

.help-topic-link:hover {
  background: var(--color-surface-hover);
}

.help-topic-link:focus-visible {
  outline: 2px solid var(--color-ring);
  outline-offset: -2px;
}

.help-topic-link-title {
  color: var(--color-text-primary);
  font-weight: 600;
}

.help-topic-link-summary {
  color: var(--color-text-muted);
  font-size: var(--text-sm);
}

.help-empty {
  color: var(--color-text-muted);
  font-size: var(--text-sm);
  line-height: 1.5;
}

.help-topic {
  display: grid;
  gap: 14px;
  color: var(--color-text-secondary);
  font-size: var(--text-base);
  line-height: 1.6;
}

.help-back {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  justify-self: start;
  padding: 0;
  border: 0;
  background: none;
  color: var(--color-primary);
  font: inherit;
  font-size: var(--text-sm);
  font-weight: 600;
  cursor: pointer;
}

.help-back-icon {
  transform: rotate(180deg);
}

.help-topic-title {
  color: var(--color-text-primary);
  font-size: var(--text-lg);
  font-weight: 650;
  letter-spacing: -0.01em;
  line-height: 1.3;
  outline: none;
}

.help-topic-summary {
  margin-top: -8px;
  color: var(--color-text-muted);
}

.help-steps,
.help-list {
  display: grid;
  gap: 6px;
  margin: 0;
  padding-left: 22px;
}

.help-steps li::marker {
  color: var(--color-primary);
  font-weight: 650;
}

.help-terms,
.help-keys {
  display: grid;
  margin: 0;
}

.help-terms > div,
.help-keys > div {
  display: grid;
  gap: 2px;
  padding: 9px 0;
  border-bottom: 1px solid var(--color-border-subtle);
}

.help-terms dt {
  color: var(--color-text-primary);
  font-weight: 600;
}

.help-terms dd,
.help-keys dd {
  margin: 0;
}

.help-keys > div {
  grid-template-columns: minmax(120px, 38%) minmax(0, 1fr);
  align-items: baseline;
  gap: 12px;
}

.help-keys dt {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 4px;
}

.help-keys kbd {
  min-width: 24px;
  padding: 1px 7px;
  border: 1px solid var(--color-border-strong);
  border-bottom-width: 2px;
  border-radius: var(--radius-sm);
  background: var(--color-surface);
  color: var(--color-text-primary);
  font-family: var(--font-family);
  font-size: var(--text-xs);
  font-weight: 600;
  text-align: center;
}

.help-then {
  color: var(--color-text-muted);
  font-size: var(--text-xs);
}

.help-related {
  display: grid;
  gap: 4px;
  margin-top: 6px;
  padding-top: 14px;
  border-top: 1px solid var(--color-border);
}

.help-related-title {
  color: var(--color-text-muted);
  font-size: var(--text-sm);
  font-weight: 600;
}

.help-related-link {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  justify-self: start;
  min-height: 32px;
  padding: 0;
  border: 0;
  background: none;
  color: var(--color-primary);
  font: inherit;
  font-weight: 600;
  cursor: pointer;
}

.help-related-link:hover {
  text-decoration: underline;
  text-underline-offset: 3px;
}
</style>
