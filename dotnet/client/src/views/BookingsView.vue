<template>
  <AppShell title="Bookings">
    <template #actions>
      <ActionMenu :items="menuItems" />
      <button class="primary-button" type="button" aria-keyshortcuts="N" @click="openForm">
        <UiIcon name="plus" />
        Add booking
      </button>
    </template>

    <section class="dss-page">
      <Drawer :open="showForm" :title="drawerTitle" :dirty="formDirty" @close="closeForm">
        <p v-if="formError" class="drawer-form-error" role="alert">{{ formError }}</p>
        <p v-if="mergeNotice" class="drawer-form-merge" role="status">{{ mergeNotice }}</p>
        <FormProblems :problems="attempted ? problems : []" @focus="focusField" />

        <fieldset class="booking-mode">
          <legend class="section-label">Booking type</legend>
          <div class="booking-mode-options" role="radiogroup" aria-label="Booking type">
            <label class="booking-mode-option" :class="{ selected: form.mode === 'package' }">
              <input
                type="radio"
                name="booking-mode"
                value="package"
                :checked="form.mode === 'package'"
                @change="setMode('package')"
              />
              <span>Ready-made package</span>
            </label>
            <label class="booking-mode-option" :class="{ selected: form.mode === 'custom' }">
              <input
                type="radio"
                name="booking-mode"
                value="custom"
                :checked="form.mode === 'custom'"
                @change="setMode('custom')"
              />
              <span>Tailored (custom) trip</span>
            </label>
          </div>
        </fieldset>

        <div class="form-grid">
          <FormField v-if="editing" id="booking-code" label="Booking ID" wide v-slot="f">
            <input :id="f.id" :value="form.id" type="text" readonly />
          </FormField>

          <FormField
            :id="fieldId('ds')"
            label="Booking date"
            :error="errors.ds"
            :note="theirNote('ds') || dateNote"
            :note-action="theirNote('ds') ? 'Use saved value' : undefined"
            @note-action="useTheirs('ds')"
            v-slot="f"
          >
            <input
              :id="f.id"
              v-model="form.ds"
              type="date"
              required
              :aria-invalid="f.invalid ? 'true' : undefined"
              :aria-describedby="f.describedBy"
              @blur="touch('ds')"
            />
          </FormField>

          <FormField
            :id="fieldId('y')"
            label="Passengers"
            :error="errors.y"
            :hint="slotHint"
            :note="theirNote('y')"
            :note-action="theirNote('y') ? 'Use saved value' : undefined"
            @note-action="useTheirs('y')"
            v-slot="f"
          >
            <input
              :id="f.id"
              v-model.number="form.y"
              type="number"
              min="1"
              :max="slotLimit ?? undefined"
              step="1"
              inputmode="numeric"
              required
              :aria-invalid="f.invalid ? 'true' : undefined"
              :aria-describedby="f.describedBy"
              @input="applyPackage"
              @blur="touch('y')"
            />
          </FormField>

          <FormField
            :id="fieldId('client')"
            label="Client or agency partner"
            wide
            :error="errors.client"
            :note="theirNote('client')"
            :note-action="theirNote('client') ? 'Use saved value' : undefined"
            @note-action="useTheirs('client')"
            v-slot="f"
          >
            <input
              :id="f.id"
              v-model.trim="form.client"
              maxlength="120"
              list="booking-client-names"
              autocomplete="off"
              required
              :aria-invalid="f.invalid ? 'true' : undefined"
              :aria-describedby="f.describedBy"
              @blur="touch('client')"
            />
            <datalist id="booking-client-names">
              <option v-for="name in clientNames" :key="name" :value="name"></option>
            </datalist>
          </FormField>

          <FormField
            v-if="form.mode === 'package'"
            :id="fieldId('packageId')"
            label="Package"
            wide
            :error="errors.packageId"
            :hint="packageHint"
            :note="theirNote('packageId')"
            :note-action="theirNote('packageId') ? 'Use saved value' : undefined"
            @note-action="useTheirs('packageId')"
            v-slot="f"
          >
            <select
              :id="f.id"
              v-model="form.packageId"
              required
              :aria-invalid="f.invalid ? 'true' : undefined"
              :aria-describedby="f.describedBy"
              @change="onPackageChange"
            >
              <option v-if="!form.packageId" :value="null" disabled>Choose a package</option>
              <option v-for="choice in packageChoices" :key="choice.id" :value="choice.id" :disabled="choice.full">
                {{ choice.label }}
              </option>
            </select>
          </FormField>

          <template v-else>
            <FormField
              :id="fieldId('package')"
              label="Trip or quotation name"
              wide
              :error="errors.package"
              :note="theirNote('package')"
              :note-action="theirNote('package') ? 'Use saved value' : undefined"
              @note-action="useTheirs('package')"
              v-slot="f"
            >
              <input
                :id="f.id"
                v-model.trim="form.package"
                maxlength="120"
                placeholder="e.g. Coron island-hopping getaway"
                required
                :aria-invalid="f.invalid ? 'true' : undefined"
                :aria-describedby="f.describedBy"
                @blur="touch('package')"
              />
            </FormField>
            <FormField
              :id="fieldId('destination')"
              label="Destination"
              wide
              :error="errors.destination"
              :note="theirNote('destination')"
              :note-action="theirNote('destination') ? 'Use saved value' : undefined"
              @note-action="useTheirs('destination')"
              v-slot="f"
            >
              <input
                :id="f.id"
                v-model.trim="form.destination"
                maxlength="120"
                list="booking-destinations"
                autocomplete="off"
                placeholder="e.g. Coron, Palawan"
                required
                :aria-invalid="f.invalid ? 'true' : undefined"
                :aria-describedby="f.describedBy"
                @blur="touch('destination')"
              />
              <datalist id="booking-destinations">
                <option v-for="place in destinationNames" :key="place" :value="place"></option>
              </datalist>
            </FormField>
          </template>

          <FormField
            :id="fieldId('grossRevenue')"
            label="Price"
            :error="errors.grossRevenue"
            :hint="priceHint"
            :note="theirNote('grossRevenue') || priceNote"
            :note-action="theirNote('grossRevenue') ? 'Use saved value' : undefined"
            @note-action="useTheirs('grossRevenue')"
            v-slot="f"
          >
            <MoneyField
              :id="f.id"
              v-model="form.grossRevenue"
              :invalid="f.invalid"
              :described-by="f.describedBy"
              @blur="touch('grossRevenue')"
            />
          </FormField>

          <FormField
            :id="fieldId('paymentStatus')"
            label="Payment status"
            :note="theirNote('paymentStatus')"
            :note-action="theirNote('paymentStatus') ? 'Use saved value' : undefined"
            @note-action="useTheirs('paymentStatus')"
            v-slot="f"
          >
            <select :id="f.id" v-model="form.paymentStatus" :aria-describedby="f.describedBy">
              <option value="Paid">Paid</option>
              <option value="Partially Paid">Partially paid</option>
              <option value="Pending">Pending</option>
            </select>
          </FormField>

          <FormField
            :id="fieldId('bookingStatus')"
            label="Booking status"
            wide
            :note="theirNote('bookingStatus')"
            :note-action="theirNote('bookingStatus') ? 'Use saved value' : undefined"
            @note-action="useTheirs('bookingStatus')"
            v-slot="f"
          >
            <select :id="f.id" v-model="form.bookingStatus" :aria-describedby="f.describedBy">
              <option value="Confirmed">Confirmed</option>
              <option value="Reserved">Reserved</option>
              <option value="Pending">Pending</option>
            </select>
          </FormField>

          <FormField
            :id="fieldId('notes')"
            label="Notes"
            optional
            wide
            :note="theirNote('notes')"
            :note-action="theirNote('notes') ? 'Use saved value' : undefined"
            @note-action="useTheirs('notes')"
            v-slot="f"
          >
            <input
              :id="f.id"
              v-model.trim="form.notes"
              maxlength="240"
              placeholder="Anything the team should know"
              :aria-describedby="f.describedBy"
            />
          </FormField>
        </div>

        <template #footer="{ requestClose }">
          <button class="secondary-button" type="button" :disabled="saving" @click="requestClose">Cancel</button>
          <button class="primary-button" type="button" :disabled="saving" @click="save">
            {{ saveLabel }}
          </button>
        </template>
      </Drawer>

      <Drawer :open="showImport" title="Import bookings from a file" @close="showImport = false">
        <p class="import-hint">
          Export the sheet as a CSV file with the columns date, client, package, destination,
          passengers, and revenue. Code, payment, status, staff, and notes are optional, and
          extra columns are ignored.
        </p>

        <p v-if="importError" class="drawer-form-error" role="alert">{{ importError }}</p>

        <div class="import-pick">
          <input
            ref="importFileInput"
            class="visually-hidden"
            type="file"
            accept=".csv,text/csv"
            @change="pickImportFile"
          />
          <button class="secondary-button" type="button" :disabled="importBusy" @click="importFileInput?.click()">
            {{ importFile ? 'Choose a different file' : 'Choose CSV file' }}
          </button>
          <span v-if="importFile" class="import-filename">{{ importFile.name }}</span>
        </div>

        <template v-if="importPreviewData">
          <dl class="import-summary">
            <div>
              <dt>Will be imported</dt>
              <dd>{{ count(importable) }}</dd>
            </div>
            <div v-if="importPreviewData.duplicates">
              <dt>Already in the system</dt>
              <dd>{{ count(importPreviewData.duplicates) }}</dd>
            </div>
            <div v-if="importPreviewData.skipped">
              <dt>Rows that could not be read</dt>
              <dd>{{ count(importPreviewData.skipped) }}</dd>
            </div>
            <div v-if="importPreviewData.from">
              <dt>Covers</dt>
              <dd>
                {{ importPreviewData.from }} to {{ importPreviewData.to }}
                ({{ importPreviewData.months }} month{{ importPreviewData.months === 1 ? '' : 's' }})
              </dd>
            </div>
            <div>
              <dt>Passengers</dt>
              <dd>{{ count(importPreviewData.passengers) }}</dd>
            </div>
            <div>
              <dt>Total revenue</dt>
              <dd>{{ peso(importPreviewData.totalRevenue) }}</dd>
            </div>
          </dl>

          <div v-if="importPreviewData.warnings.length" class="import-notes import-warnings">
            <p class="import-notes-title">Read with a caution</p>
            <p v-for="w in importPreviewData.warnings.slice(0, 8)" :key="'w' + w.line">
              Line {{ w.line }}: {{ w.reason }}
            </p>
            <p v-if="importPreviewData.warnings.length > 8">
              and {{ importPreviewData.warnings.length - 8 }} more.
            </p>
          </div>

          <div v-if="importPreviewData.problems.length" class="import-notes import-problems">
            <p class="import-notes-title">Rows that will be skipped</p>
            <p v-for="p in importPreviewData.problems.slice(0, 8)" :key="'p' + p.line">
              Line {{ p.line }}: {{ p.reason }}
            </p>
            <p v-if="importPreviewData.problems.length > 8">
              and {{ importPreviewData.problems.length - 8 }} more.
            </p>
          </div>

          <p class="import-hint">
            Nothing is saved until you press import. Imported bookings are recorded as history
            and do not take slots from packages on sale today.
          </p>
        </template>

        <template #footer>
          <button class="secondary-button" type="button" :disabled="importBusy" @click="showImport = false">Cancel</button>
          <button
            class="primary-button"
            type="button"
            :disabled="importBusy || importable === 0"
            @click="commitImport"
          >
            {{ importBusy ? 'Working…' : importable > 0
              ? 'Import ' + count(importable) + ' booking' + (importable === 1 ? '' : 's')
              : 'Nothing to import' }}
          </button>
        </template>
      </Drawer>

      <BookingDetail
        :open="detailOpen"
        :booking="detailBooking"
        :can-see-history="canSeeHistory"
        :history-version="historyVersion"
        :busy="detailBusy"
        :void-error="detailVoidError"
        :action-error="detailActionError"
        @close="detailOpen = false"
        @edit="editFromDetail"
        @mark-paid="quickUpdate('paid')"
        @confirm="quickUpdate('confirm')"
        @void="voidFromDetail"
        @restore="restoreFromDetail"
      />

      <MonthAgainstForecast
        v-if="!loading && businessToday"
        :recorded="monthToDate"
        :recorded-bookings="bookingsThisMonth"
        :today="businessToday"
      />

      <div v-if="loading" class="content-panel">
        <Skeleton :rows="8" />
      </div>

      <div v-else-if="loadError" class="content-panel load-error" role="alert">
        <p class="load-error-title">The bookings could not be loaded</p>
        <p class="load-error-text">Check the connection, then try again. Nothing has been changed.</p>
        <button class="secondary-button" type="button" @click="retryLoad">Try again</button>
      </div>
      <EmptyState
        v-else-if="!bookings.length"
        title="No bookings yet"
        message="Record your first booking or quotation to start tracking passengers and revenue."
      >
        <template #action>
          <button class="primary-button" type="button" @click="openForm">Add booking</button>
        </template>
      </EmptyState>

      <section v-else class="content-panel records-panel" aria-label="Bookings">
        <div class="records-tools">
          <SearchField v-model="filters.q" placeholder="Search client, package, destination or ID" />
          <PeriodSelect
            v-model:period="periodKey"
            v-model:from="filters.from"
            v-model:to="filters.to"
            :today="businessToday"
          />
          <label v-if="stacked" class="records-sort">
            <span class="visually-hidden">Sort bookings</span>
            <select v-model="filters.sort">
              <option v-for="option in sortOptions" :key="option.key" :value="option.key">{{ option.label }}</option>
            </select>
          </label>
          <button
            v-if="stacked"
            type="button"
            class="filter-chip select-toggle"
            :aria-pressed="selecting"
            @click="toggleSelecting"
          >
            {{ selecting ? 'Done selecting' : 'Select' }}
          </button>
        </div>

        <div class="records-filters" role="group" aria-label="Filter by payment">
          <button
            v-for="option in paymentOptions"
            :key="option.key"
            type="button"
            class="filter-chip"
            :aria-pressed="filters.payment === option.key"
            @click="filters.payment = option.key"
          >
            {{ option.label }}
            <span class="chip-count">{{ count(paymentCounts[option.key] ?? 0) }}</span>
          </button>
          <template v-if="voidedInScope > 0">
            <span class="chip-divider" aria-hidden="true"></span>
            <button
              type="button"
              class="filter-chip"
              :aria-pressed="showVoided"
              @click="filters.voided = showVoided ? '' : '1'"
            >
              Show voided
              <span class="chip-count">{{ count(voidedInScope) }}</span>
            </button>
          </template>
        </div>

        <p class="records-summary" aria-live="polite">
          <span class="summary-figure"><strong>{{ count(active.length) }}</strong> {{ active.length === 1 ? 'booking' : 'bookings' }}</span>
          <span class="summary-figure"><strong>{{ count(activePassengers) }}</strong> {{ activePassengers === 1 ? 'passenger' : 'passengers' }}</span>
          <span class="summary-figure"><strong>{{ peso(activeRevenue) }}</strong> revenue</span>
          <span v-if="filters.payment === 'all' && unpaidInView.length" class="summary-figure tone-warning">
            <strong>{{ peso(unpaidValue) }}</strong> in {{ count(unpaidInView.length) }} unpaid {{ unpaidInView.length === 1 ? 'booking' : 'bookings' }}
          </span>
          <span class="summary-scope">{{ scopeText }}</span>
          <button v-if="isFiltered" class="table-link" type="button" @click="clearFilters">Clear filters</button>
        </p>

        <template v-if="matching.length">
          <ul v-if="stacked" class="record-list">
            <template v-for="section in sections" :key="section.key">
              <li v-if="section.label && section.summary" class="record-list-month">
                <span>{{ section.label }}</span>
                <span>{{ count(section.summary.count) }} {{ section.summary.count === 1 ? 'booking' : 'bookings' }} · {{ peso(section.summary.revenue) }}</span>
              </li>
              <li v-for="b in section.items" :key="b.id">
                <button
                  class="record-item"
                  :class="{ 'is-voided': b.voided, selectable: selecting, 'is-selected': selected.has(b.id) }"
                  type="button"
                  :aria-pressed="selecting ? selected.has(b.id) : undefined"
                  :disabled="selecting && b.voided"
                  @click="selecting ? toggle(b) : openDetail(b)"
                >
                  <span v-if="selecting" class="record-check" aria-hidden="true">
                    <UiIcon v-if="selected.has(b.id)" name="check" :stroke-width="2.6" />
                  </span>
                  <span class="record-item-top">
                    <span class="record-item-title">{{ b.client }}</span>
                    <span class="record-item-figure">{{ peso(b.grossRevenue) }}</span>
                  </span>
                  <span class="record-item-sub">{{ tripLabel(b) }}</span>
                  <span class="record-item-meta">
                    <span>{{ dateLabel(b.ds) }} · {{ b.id }} · {{ b.y }} pax<template v-if="b.voided"> · Voided</template></span>
                    <span class="record-badge" :class="badge(b.paymentStatus)">{{ stateLabel(b.paymentStatus) }}</span>
                  </span>
                </button>
              </li>
            </template>
          </ul>

          <div v-else class="dss-table-frame">
            <table class="dss-table records-table">
              <thead>
                <tr>
                  <th scope="col" class="select-cell">
                    <input
                      type="checkbox"
                      :checked="allShownSelected"
                      :indeterminate.prop="someShownSelected && !allShownSelected"
                      :disabled="selectableShown.length === 0"
                      :aria-label="`Select all ${selectableShown.length} bookings shown`"
                      @change="toggleAllShown"
                    />
                  </th>
                  <SortHeader label="Date" field="date" :sort="filters.sort" @sort="sortBy" />
                  <SortHeader label="Client" field="client" :sort="filters.sort" @sort="sortBy" />
                  <th scope="col">Trip</th>
                  <SortHeader label="Passengers" field="pax" align="right" :sort="filters.sort" @sort="sortBy" />
                  <SortHeader label="Revenue" field="revenue" align="right" :sort="filters.sort" @sort="sortBy" />
                  <th scope="col">Payment</th>
                  <th scope="col">Status</th>
                </tr>
              </thead>
              <tbody v-for="section in sections" :key="section.key">
                <tr v-if="section.label && section.summary" class="month-row">
                  <th scope="rowgroup" colspan="4">
                    {{ section.label }}
                    <span class="month-count">· {{ count(section.summary.count) }} {{ section.summary.count === 1 ? 'booking' : 'bookings' }}</span>
                  </th>
                  <td class="num">{{ count(section.summary.passengers) }}</td>
                  <td class="num">{{ peso(section.summary.revenue) }}</td>
                  <td colspan="2"></td>
                </tr>
                <tr
                  v-for="b in section.items"
                  :key="b.id"
                  class="record-row"
                  :class="{ 'is-voided': b.voided, 'is-selected': selected.has(b.id) }"
                  @click="openDetail(b)"
                >
                  <td class="select-cell" @click.stop>
                    <input
                      type="checkbox"
                      :checked="selected.has(b.id)"
                      :disabled="b.voided"
                      :aria-label="`Select booking ${b.id}, ${b.client}`"
                      @click="onCheck(b, $event)"
                    />
                  </td>
                  <td class="cell-date">{{ dateLabel(b.ds) }}</td>
                  <td>
                    <button class="row-open" type="button" @click.stop="openDetail(b)">{{ b.client }}</button>
                    <span class="row-subtext">
                      {{ b.id }}<span v-if="b.voided" class="voided-tag" :title="b.voidReason ?? ''">Voided</span>
                    </span>
                  </td>
                  <td>
                    <span class="trip-name">{{ b.package }}</span>
                    <span class="row-subtext">
                      {{ b.destination }}<span v-if="isTailored(b)" class="kind-tag">Tailored</span>
                    </span>
                  </td>
                  <td class="num">{{ b.y }}</td>
                  <td class="num"><strong>{{ peso(b.grossRevenue) }}</strong></td>
                  <td><span class="record-badge" :class="badge(b.paymentStatus)">{{ stateLabel(b.paymentStatus) }}</span></td>
                  <td><span class="record-badge" :class="badge(b.bookingStatus)">{{ stateLabel(b.bookingStatus) }}</span></td>
                </tr>
              </tbody>
            </table>
          </div>

          <ListFooter
            :shown="shown"
            :total="total"
            :page-size="PAGE"
            noun="bookings"
            @more="loadMore"
            @all="showAll"
          />

          <SelectionBar :count="selected.size" :detail="selectionDetail" @clear="clearSelection">
            <button
              class="primary-button compact-button"
              type="button"
              :disabled="bulkBusy !== '' || selectedUnpaid.length === 0"
              @click="bulkUpdate('paid')"
            >
              <UiIcon name="wallet" />
              {{ bulkBusy === 'paid' ? 'Saving…' : selectedUnpaid.length ? `Mark ${selectedUnpaid.length} as paid` : 'All paid' }}
            </button>
            <button
              class="secondary-button compact-button"
              type="button"
              :disabled="bulkBusy !== '' || selectedUnconfirmed.length === 0"
              @click="bulkUpdate('confirm')"
            >
              <UiIcon name="check" />
              {{ bulkBusy === 'confirm' ? 'Saving…' : selectedUnconfirmed.length ? `Confirm ${selectedUnconfirmed.length}` : 'All confirmed' }}
            </button>
          </SelectionBar>
        </template>

        <div v-else class="records-empty">
          <strong>No bookings match</strong>
          <span>Try a different search, payment status or period.</span>
          <button class="table-link" type="button" @click="clearFilters">Clear filters</button>
        </div>
      </section>
    </section>
  </AppShell>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import AppShell from '../components/AppShell.vue';
import ActionMenu, { type ActionMenuItem } from '../components/ActionMenu.vue';
import BookingDetail, { type BookingBusy } from '../components/BookingDetail.vue';
import Drawer from '../components/Drawer.vue';
import Skeleton from '../components/Skeleton.vue';
import EmptyState from '../components/EmptyState.vue';
import SearchField from '../components/SearchField.vue';
import ListFooter from '../components/ListFooter.vue';
import SelectionBar from '../components/SelectionBar.vue';
import PeriodSelect from '../components/PeriodSelect.vue';
import SortHeader from '../components/SortHeader.vue';
import UiIcon from '../components/UiIcon.vue';
import MoneyField from '../components/MoneyField.vue';
import FormField from '../components/FormField.vue';
import FormProblems from '../components/FormProblems.vue';
import MonthAgainstForecast from '../components/forecast/MonthAgainstForecast.vue';
import { invalidateForecast } from '../composables/useForecast';
import { usePaged } from '../composables/usePaged';
import { undoAction, useToast } from '../composables/useToast';
import { useMediaQuery } from '../composables/useMediaQuery';
import { useQueryState } from '../composables/useQueryState';
import { useFieldErrors } from '../composables/useFieldErrors';
import { useAuth } from '../stores/auth';
import { canAccess } from '../nav';
import { api, ApiError, type Booking, type BookingInput, type ImportPreview, type PackageOption } from '../api';
import { count, peso, stateLabel } from '../format';
import { describeRange, formatDate, formatDay, inRange, isIsoDate, isPeriodKey, resolvePeriod, type PeriodKey } from '../periods';
import { compareText, monthSections, nextSort, singleSection, sortParts, type SortDirection } from '../listing';
import { isAfter, listNames, mergeDraft, nextCode, suggestions } from '../records';
import { rememberRecent } from '../recents';

type BookingMode = 'package' | 'custom';
type BookingForm = BookingInput & { mode: BookingMode };
type BookingRecovery = { kind: 'create'; payload: BookingInput } | { kind: 'stale-edit' };

const PAGE = 50;

const toast = useToast();
const route = useRoute();
const router = useRouter();
const { state: auth } = useAuth();

const bookings = ref<Booking[]>([]);
const packages = ref<PackageOption[]>([]);
const loading = ref(true);
const showForm = ref(false);
const saving = ref(false);
const formError = ref('');
const editing = ref(false);
const originalCode = ref('');
const drawerTitle = ref('New booking');
// Set when the form was opened from a booking's panel, so closing the form goes back to it.
const returnToDetail = ref(false);

// Set once someone has been told the price is far above the usual, so the next save confirms it.
const confirmUnusual = ref(false);
// The server's own wording, when it queried the price before the form did.
const serverUnusual = ref('');

const showImport = ref(false);
const importFileInput = ref<HTMLInputElement | null>(null);
const importFile = ref<File | null>(null);
const importPreviewData = ref<ImportPreview | null>(null);
const importBusy = ref(false);
const importError = ref('');

const importable = computed(() => {
  const p = importPreviewData.value;
  return p ? Math.max(0, p.valid - p.duplicates) : 0;
});

const pendingRecovery = ref<BookingRecovery | null>(null);
const createRecoveryError = 'Could not confirm whether this booking was saved. Check your connection, then try again before changing this form.';
const staleRecoveryError = 'Could not reload the latest booking after a conflict. Check your connection, then try again before saving.';

const businessToday = ref('');
const businessDateError = 'Could not load the business date. Refresh and try again.';
const form = reactive<BookingForm>(blank());

// The booking panel
const detailOpen = ref(false);
const detailCode = ref<string | null>(null);
const detailBooking = computed(() => bookings.value.find((b) => b.id === detailCode.value) ?? null);
const detailBusy = ref<BookingBusy>('');
const detailVoidError = ref('');
const detailActionError = ref('');
const historyVersion = ref(0);
// The change history comes from the activity log, which Owner and Admin can read.
const canSeeHistory = computed(() => canAccess(auth.user?.role, '/reports'));

const menuItems: ActionMenuItem[] = [
  {
    label: 'Export bookings',
    hint: 'Every booking as a CSV file, voided ones marked',
    icon: 'download',
    href: '/api/export/bookings.csv',
  },
  { label: 'Import from a file', hint: 'Add bookings from a CSV sheet', icon: 'upload', run: openImport },
];

// ----- Filters, kept in the address -----

const paymentOptions = [
  { key: 'all', label: 'All' },
  // Unpaid matches what Home counts: every active booking that is not fully paid.
  { key: 'unpaid', label: 'Unpaid' },
  { key: 'paid', label: 'Paid' },
  { key: 'partially-paid', label: 'Partially paid' },
  { key: 'pending', label: 'Pending' },
];

const sortOptions = [
  { key: 'date-desc', label: 'Newest first' },
  { key: 'date-asc', label: 'Oldest first' },
  { key: 'revenue-desc', label: 'Highest revenue' },
  { key: 'revenue-asc', label: 'Lowest revenue' },
  { key: 'pax-desc', label: 'Most passengers' },
  { key: 'pax-asc', label: 'Fewest passengers' },
  { key: 'client-asc', label: 'Client A to Z' },
  { key: 'client-desc', label: 'Client Z to A' },
];

const firstDirection: Record<string, SortDirection> = { date: 'desc', client: 'asc', pax: 'desc', revenue: 'desc' };

const filters = useQueryState(
  { q: '', payment: 'all', period: 'all', from: '', to: '', voided: '', sort: 'date-desc' },
  {
    payment: (value) => paymentOptions.some((option) => option.key === value),
    period: isPeriodKey,
    from: isIsoDate,
    to: isIsoDate,
    voided: (value) => value === '1',
    sort: (value) => sortOptions.some((option) => option.key === value),
  },
);

const periodKey = computed<PeriodKey>({
  get: () => (isPeriodKey(filters.period) ? filters.period : 'all'),
  set: (value) => {
    filters.period = value;
    if (value !== 'custom') {
      filters.from = '';
      filters.to = '';
    }
  },
});

const showVoided = computed(() => filters.voided === '1');
const stacked = useMediaQuery('(max-width: 860px)');

const range = computed(() => resolvePeriod(periodKey.value, businessToday.value, filters.from, filters.to));

const searched = computed(() => {
  const term = filters.q.trim().toLowerCase();
  return bookings.value.filter((b) => {
    if (!inRange(b.ds, range.value)) return false;
    if (!term) return true;
    return [b.client, b.package, b.destination, b.id].some((field) => (field ?? '').toLowerCase().includes(term));
  });
});

const voidedInScope = computed(() => searched.value.filter((b) => b.voided).length);
const pool = computed(() => (showVoided.value ? searched.value : searched.value.filter((b) => !b.voided)));

function isUnpaid(b: Booking): boolean {
  return !b.voided && b.paymentStatus !== 'Paid';
}

function paymentMatches(b: Booking, key: string): boolean {
  switch (key) {
    case 'all':
      return true;
    case 'unpaid':
      return isUnpaid(b);
    case 'paid':
      return b.paymentStatus === 'Paid';
    case 'partially-paid':
      return b.paymentStatus === 'Partially Paid';
    case 'pending':
      return b.paymentStatus === 'Pending';
    default:
      return true;
  }
}

const paymentCounts = computed(() => {
  const counts: Record<string, number> = {};
  for (const option of paymentOptions) {
    counts[option.key] = pool.value.filter((b) => paymentMatches(b, option.key)).length;
  }
  return counts;
});

const matching = computed(() => pool.value.filter((b) => paymentMatches(b, filters.payment)));

// Revenue is gross booking revenue with voided bookings left out, the same figure Home and
// Reports use. Voided bookings can be listed, but they never count.
const active = computed(() => matching.value.filter((b) => !b.voided));
const activePassengers = computed(() => active.value.reduce((sum, b) => sum + b.y, 0));
const activeRevenue = computed(() => active.value.reduce((sum, b) => sum + b.grossRevenue, 0));
// The booked value of bookings not yet fully paid. Part-payments are not recorded, so this is
// the bookings' value, not the balance still owed.
const unpaidInView = computed(() => active.value.filter(isUnpaid));
const unpaidValue = computed(() => unpaidInView.value.reduce((sum, b) => sum + b.grossRevenue, 0));

// This month's bookings so far, voided ones left out, for the forecast band.
const activeThisMonth = computed(() => {
  const month = businessToday.value.slice(0, 7);
  return month ? bookings.value.filter((b) => !b.voided && b.ds.startsWith(month)) : [];
});
const monthToDate = computed(() => activeThisMonth.value.reduce((sum, b) => sum + b.grossRevenue, 0));
const bookingsThisMonth = computed(() => activeThisMonth.value.length);

const sorted = computed(() => {
  const { field, direction } = sortParts(filters.sort);
  const sign = direction === 'asc' ? 1 : -1;
  return [...matching.value].sort((a, b) => {
    let order = 0;
    if (field === 'client') order = compareText(a.client, b.client);
    else if (field === 'pax') order = a.y - b.y;
    else if (field === 'revenue') order = a.grossRevenue - b.grossRevenue;
    if (order === 0) order = a.ds < b.ds ? -1 : a.ds > b.ds ? 1 : a.backendId - b.backendId;
    return order * sign;
  });
});

const { visible, total, shown, loadMore, showAll } = usePaged(sorted, PAGE);

const byDate = computed(() => sortParts(filters.sort).field === 'date');

interface MonthTotals {
  count: number;
  passengers: number;
  revenue: number;
}

function totalsOf(items: Booking[]): MonthTotals {
  const counted = items.filter((b) => !b.voided);
  return {
    count: counted.length,
    passengers: counted.reduce((sum, b) => sum + b.y, 0),
    revenue: counted.reduce((sum, b) => sum + b.grossRevenue, 0),
  };
}

const sections = computed(() =>
  byDate.value
    ? monthSections(visible.value, sorted.value, (b) => b.ds, totalsOf)
    : singleSection<Booking, MonthTotals>(visible.value),
);

const isFiltered = computed(
  () => filters.q.trim() !== '' || filters.payment !== 'all' || periodKey.value !== 'all' || showVoided.value,
);

const scopeText = computed(() => {
  const parts = [range.value ? describeRange(range.value) : 'All dates'];
  const term = filters.q.trim();
  if (term) parts.push(`matching “${term}”`);
  const listedVoided = matching.value.length - active.value.length;
  if (listedVoided > 0) parts.push(`${listedVoided} voided listed, not counted`);
  return parts.join(' · ');
});

function sortBy(field: string) {
  filters.sort = nextSort(filters.sort, field, firstDirection[field] ?? 'asc');
}

function clearFilters() {
  filters.q = '';
  filters.payment = 'all';
  periodKey.value = 'all';
  filters.voided = '';
}

function dateLabel(ds: string): string {
  // Inside a month heading the year is already on screen.
  return byDate.value ? formatDay(ds) : formatDate(ds);
}

function tripLabel(b: Booking): string {
  return b.destination ? `${b.package} · ${b.destination}` : b.package;
}

function badge(value: string): string {
  return 'status-' + value.toLowerCase().replace(/[^a-z0-9]+/g, '-');
}

function isTailored(b: Booking): boolean {
  return b.packageId == null;
}

// ----- Selecting several bookings -----

const selected = reactive(new Set<string>());
const selecting = ref(false);
const bulkBusy = ref<'' | 'paid' | 'confirm'>('');
let lastChecked: string | null = null;

const selectableShown = computed(() => visible.value.filter((b) => !b.voided));
const allShownSelected = computed(() => selectableShown.value.length > 0 && selectableShown.value.every((b) => selected.has(b.id)));
const someShownSelected = computed(() => selectableShown.value.some((b) => selected.has(b.id)));
const selectedBookings = computed(() => bookings.value.filter((b) => selected.has(b.id) && !b.voided));
const selectedUnpaid = computed(() => selectedBookings.value.filter((b) => b.paymentStatus !== 'Paid'));
const selectedUnconfirmed = computed(() => selectedBookings.value.filter((b) => b.bookingStatus !== 'Confirmed'));
const selectionDetail = computed(() => {
  const value = selectedBookings.value.reduce((sum, b) => sum + b.grossRevenue, 0);
  return `${peso(value)} booked`;
});

function toggle(b: Booking) {
  if (b.voided) return;
  if (selected.has(b.id)) selected.delete(b.id);
  else selected.add(b.id);
  lastChecked = b.id;
}

/** Shift-click selects every booking between the last one ticked and this one. */
function onCheck(b: Booking, event: MouseEvent) {
  const order = visible.value;
  const from = lastChecked ? order.findIndex((item) => item.id === lastChecked) : -1;
  const to = order.findIndex((item) => item.id === b.id);
  if (event.shiftKey && from >= 0 && to >= 0) {
    const turnOn = !selected.has(b.id);
    for (const item of order.slice(Math.min(from, to), Math.max(from, to) + 1)) {
      if (item.voided) continue;
      if (turnOn) selected.add(item.id);
      else selected.delete(item.id);
    }
    lastChecked = b.id;
    return;
  }
  toggle(b);
}

function toggleAllShown() {
  if (allShownSelected.value) for (const b of selectableShown.value) selected.delete(b.id);
  else for (const b of selectableShown.value) selected.add(b.id);
}

function clearSelection() {
  selected.clear();
  lastChecked = null;
}

function toggleSelecting() {
  selecting.value = !selecting.value;
  if (!selecting.value) clearSelection();
}

// A filter that hides a booking also lets go of it, so nothing unseen is changed.
watch(matching, (list) => {
  const still = new Set(list.map((b) => b.id));
  for (const id of [...selected]) if (!still.has(id)) selected.delete(id);
});

async function bulkUpdate(action: 'paid' | 'confirm') {
  if (bulkBusy.value) return;
  const targets = action === 'paid' ? selectedUnpaid.value : selectedUnconfirmed.value;
  if (targets.length === 0) return;
  bulkBusy.value = action;
  const before = targets.map((b) => ({ ...b }));
  const revisions = Object.fromEntries(targets.map((b) => [b.id, b.revision]));
  try {
    const result = await api.bulkBookings(targets.map((b) => b.id), action, revisions);
    await load(false);
    historyVersion.value += 1;
    clearSelection();
    selecting.value = false;
    const n = result.updated;
    const noun = n === 1 ? 'booking' : 'bookings';
    toast.success(action === 'paid' ? `${n} ${noun} marked as paid` : `${n} ${noun} confirmed`, {
      action: undoAction(() => runUndo(
        'One of these bookings',
        () => restoreStates(before, result.bookings),
        `${n} ${noun} put back as they were`,
      )),
    });
  } catch (e) {
    await load(false);
    toast.error(e instanceof ApiError && e.status === 409
      ? 'Some of these bookings changed or were voided while selected. The list is up to date now; check the selection and try again.'
      : 'Could not update the bookings. Check the connection, then try again.');
  } finally {
    bulkBusy.value = '';
  }
}

// ----- The booking panel -----

function openDetail(b: Booking) {
  detailCode.value = b.id;
  detailVoidError.value = '';
  detailActionError.value = '';
  detailOpen.value = true;
  rememberRecent({ kind: 'booking', code: b.id, label: b.client, detail: `${b.id} · ${b.package} · ${peso(b.grossRevenue)}` });
}

function editFromDetail() {
  const b = detailBooking.value;
  if (!b) return;
  detailOpen.value = false;
  openEdit(b);
  returnToDetail.value = true;
}

function bookingInput(b: Booking, revision: number): BookingInput {
  return {
    id: b.id,
    ds: b.ds,
    y: b.y,
    client: b.client,
    package: b.package,
    packageId: b.packageId,
    entryType: b.entryType,
    destination: b.destination,
    grossRevenue: b.grossRevenue,
    paymentStatus: b.paymentStatus,
    bookingStatus: b.bookingStatus,
    staffAssigned: b.staffAssigned,
    source: b.source,
    notes: b.notes,
    revision,
    // Putting back an amount that was already accepted is not a new question.
    confirmUnusual: true,
  };
}

/** Runs an undo; refused by the server when the booking has changed since, which is said plainly. */
async function runUndo(subject: string, steps: () => Promise<unknown>, done: string) {
  try {
    await steps();
    invalidateForecast();
    await load(false);
    historyVersion.value += 1;
    toast.success(done);
  } catch (e) {
    await load(false);
    const stale = e instanceof ApiError && e.status === 409 && (e.code === 'stale_revision' || e.code === 'write_conflict');
    toast.error(stale
      ? `${subject} changed after that, so the change could not be undone. Open it to change it by hand.`
      : e instanceof ApiError ? Object.values(e.fields)[0] ?? e.message : 'Could not undo the change. Check the connection, then change the booking by hand.');
  }
}

/** Puts each booking back as it was, in turn, against the revision the change left it at. */
async function restoreStates(before: Booking[], after: Booking[]) {
  for (const old of before) {
    const now = after.find((b) => b.id === old.id);
    if (now) await api.updateBooking(old.id, bookingInput(old, now.revision));
  }
}

async function quickUpdate(action: 'paid' | 'confirm') {
  const b = detailBooking.value;
  if (!b || detailBusy.value) return;
  detailBusy.value = action;
  detailActionError.value = '';
  const before = { ...b };
  try {
    const result = await api.bulkBookings([b.id], action, { [b.id]: b.revision });
    await load(false);
    historyVersion.value += 1;
    const back = action === 'paid' ? `Booking ${b.id} is ${stateLabel(before.paymentStatus).toLowerCase()} again` : `Booking ${b.id} is ${stateLabel(before.bookingStatus).toLowerCase()} again`;
    toast.success(action === 'paid' ? `Booking ${b.id} marked as paid` : `Booking ${b.id} confirmed`, {
      action: undoAction(() => runUndo(`Booking ${b.id}`, () => restoreStates([before], result.bookings), back)),
    });
  } catch (e) {
    if (e instanceof ApiError && e.status === 409) {
      await load(false);
      detailActionError.value = 'This booking changed while it was open. The latest details are shown; try again if it still needs the update.';
    } else {
      detailActionError.value = e instanceof ApiError ? Object.values(e.fields)[0] ?? e.message : 'Could not update the booking.';
    }
  } finally {
    detailBusy.value = '';
  }
}

async function voidFromDetail(reason: string) {
  const b = detailBooking.value;
  if (!b || detailBusy.value || !reason) return;
  detailBusy.value = 'void';
  detailVoidError.value = '';
  try {
    const after = await api.voidBooking(b.id, reason, b.revision);
    invalidateForecast();
    await load(false);
    historyVersion.value += 1;
    toast.success('Booking ' + b.id + ' voided', {
      action: undoAction(() => runUndo(`Booking ${b.id}`, () => api.restoreBooking(b.id, after.revision), `Booking ${b.id} restored`)),
    });
  } catch (e) {
    if (e instanceof ApiError && e.status === 409) await load(false);
    detailVoidError.value = e instanceof ApiError ? conflictMessage(e, 'booking') : 'Could not void the booking.';
  } finally {
    detailBusy.value = '';
  }
}

async function restoreFromDetail() {
  const b = detailBooking.value;
  if (!b || detailBusy.value) return;
  detailBusy.value = 'restore';
  detailActionError.value = '';
  const reason = b.voidReason || 'Voided again after a restore';
  try {
    const after = await api.restoreBooking(b.id, b.revision);
    invalidateForecast();
    await load(false);
    historyVersion.value += 1;
    toast.success('Booking ' + b.id + ' restored', {
      action: undoAction(() => runUndo(`Booking ${b.id}`, () => api.voidBooking(b.id, reason, after.revision), `Booking ${b.id} voided again`)),
    });
  } catch (e) {
    if (e instanceof ApiError && e.status === 409) await load(false);
    detailActionError.value = e instanceof ApiError ? conflictMessage(e, 'booking') : 'Could not restore the booking.';
  } finally {
    detailBusy.value = '';
  }
}

// ----- The booking form -----

const FIELD_LABELS = {
  ds: 'Booking date',
  y: 'Passengers',
  client: 'Client or agency partner',
  packageId: 'Package',
  package: 'Trip or quotation name',
  destination: 'Destination',
  grossRevenue: 'Price',
  paymentStatus: 'Payment status',
  bookingStatus: 'Booking status',
  notes: 'Notes',
} as const;
type BookingField = keyof typeof FIELD_LABELS;

// Every field a save sends, for merging in someone else's changes after a conflict.
const MERGE_KEYS = [
  'ds', 'y', 'client', 'package', 'packageId', 'entryType', 'destination', 'grossRevenue',
  'paymentStatus', 'bookingStatus', 'staffAssigned', 'source', 'notes', 'mode',
] as const;

// The checks the server makes on a price, made here first so the question comes before the save.
const UNUSUAL_MULTIPLE = 5;
const UNUSUAL_SAMPLE = 8;

// The booking as it was when the form opened, and as last read after a conflict.
const original = ref<Booking | null>(null);
const baseForm = ref<BookingForm | null>(null);
// Values someone else saved for fields this form had also changed, shown beside those fields.
const theirValues = ref<Partial<Record<BookingField, unknown>>>({});
const mergeNotice = ref('');
const openedSnapshot = ref('');

const {
  errors,
  attempted,
  problems,
  touch,
  recheck,
  validateAll,
  placeServerErrors,
  reset: resetFieldErrors,
  fieldId,
  focusField,
} = useFieldErrors<BookingField>('booking', FIELD_LABELS, {
  ds: () => (form.ds ? '' : 'Choose the date the booking was made.'),
  y: () => {
    const y = form.y as unknown;
    if (typeof y !== 'number' || !Number.isInteger(y) || y < 1) return 'Enter the number of passengers, at least 1.';
    const limit = slotLimit.value;
    if (limit != null && y > limit) {
      return limit <= 0
        ? 'This package has no slots left. Choose another package or a tailored trip.'
        : `Only ${count(limit)} ${limit === 1 ? 'slot is' : 'slots are'} left on this package.`;
    }
    return '';
  },
  client: () => (form.client.trim() ? '' : 'Enter the client or agency partner.'),
  packageId: () => (form.mode === 'package' && !form.packageId ? 'Choose a package, or switch to a tailored trip.' : ''),
  package: () => (form.mode === 'custom' && !form.package.trim() ? 'Name the trip or quotation.' : ''),
  destination: () => (form.mode === 'custom' && !form.destination.trim() ? 'Enter the destination.' : ''),
  grossRevenue: () =>
    Number.isFinite(form.grossRevenue) && form.grossRevenue >= 0 ? '' : 'Enter a price of zero or more.',
});

// A field already left is checked again as it changes, so its message goes once it is right.
watch(form, recheck);
// A different price is a different question.
watch(
  () => form.grossRevenue,
  () => {
    confirmUnusual.value = false;
    serverUnusual.value = '';
  },
);

const selectedPackage = computed(() =>
  form.mode === 'package' ? packages.value.find((p) => p.id === form.packageId) ?? null : null,
);

/** Seats this booking already holds on a package; an edit can keep them. */
function heldSeats(packageId: string | null): number {
  const b = original.value;
  return b && !b.voided && b.packageId === packageId ? b.y : 0;
}

/**
 * Seats the booking can take on the chosen package: what is free plus what it already holds.
 * A voided booking holds nothing and the server does not check its seats, so neither does this.
 */
const slotLimit = computed<number | null>(() => {
  const p = selectedPackage.value;
  if (!p || original.value?.voided) return null;
  return p.availableSlots + heldSeats(p.id);
});

const packageChoices = computed(() =>
  packages.value.map((p) => {
    const free = p.availableSlots + heldSeats(p.id);
    return {
      id: p.id,
      // A full package cannot be chosen, unless it is the one already on the booking.
      full: free <= 0 && p.id !== form.packageId,
      label: `${p.packageName} · ${free <= 0 ? 'full' : `${count(free)} ${free === 1 ? 'slot' : 'slots'} left`}`,
    };
  }),
);

const slotHint = computed(() => {
  const limit = slotLimit.value;
  if (limit == null || limit <= 0) return '';
  return limit === 1 ? '1 slot left on this package' : `${count(limit)} slots left on this package`;
});

const packageHint = computed(() => {
  const p = selectedPackage.value;
  if (!p) return '';
  return [p.destination, p.duration, `${peso(p.basePrice)} per passenger`].filter(Boolean).join(' · ');
});

const priceHint = computed(() => {
  const p = selectedPackage.value;
  if (!p) return '';
  const passengers = typeof form.y === 'number' && form.y > 0 ? form.y : 0;
  const listed = passengers * p.basePrice;
  if (form.grossRevenue === listed) {
    return `${count(passengers)} × ${peso(p.basePrice)}, the package price. Change it for a discount or extras.`;
  }
  return `The package price would be ${peso(listed)}.`;
});

const dateNote = computed(() =>
  isAfter(form.ds, businessToday.value)
    ? 'This is after today. Bookings count on the day they were made, so check the date.'
    : '',
);

/** The middle booking amount, worked out the way the server does for its check. */
const usualAmount = computed<number | null>(() => {
  const others = bookings.value
    .filter((b) => b.id !== originalCode.value)
    .map((b) => b.grossRevenue)
    .sort((a, b) => a - b);
  if (others.length < UNUSUAL_SAMPLE) return null;
  const middle = Math.floor(others.length / 2);
  const median = others.length % 2 === 1 ? others[middle] : Math.trunc((others[middle - 1] + others[middle]) / 2);
  return median > 0 ? median : null;
});

const unusualAmount = computed(() => {
  const usual = usualAmount.value;
  if (usual == null) return false;
  if (original.value && original.value.grossRevenue === form.grossRevenue) return false;
  return form.grossRevenue >= usual * UNUSUAL_MULTIPLE;
});

const priceNote = computed(() => {
  if (serverUnusual.value) return serverUnusual.value;
  const usual = usualAmount.value;
  if (!unusualAmount.value || usual == null) return '';
  const said = `${peso(form.grossRevenue)} is more than ${UNUSUAL_MULTIPLE} times the usual booking (about ${peso(usual)}). Check for a stray digit`;
  return confirmUnusual.value ? `${said}, or save again to keep it.` : `${said}.`;
});

const saveLabel = computed(() => {
  if (saving.value) return 'Saving…';
  if (confirmUnusual.value && (unusualAmount.value || serverUnusual.value)) return 'Save with this price';
  return editing.value ? 'Save changes' : 'Save booking';
});

// Names already used, offered as the field is typed, so one client is not spelled three ways.
const clientNames = computed(() => suggestions(bookings.value.map((b) => b.client)));
const destinationNames = computed(() =>
  suggestions([...packages.value.map((p) => p.destination), ...bookings.value.map((b) => b.destination)]),
);

function snapshotForm(): string {
  const { revision: _revision, ...rest } = form;
  return JSON.stringify(rest);
}

const formDirty = computed(() => showForm.value && snapshotForm() !== openedSnapshot.value);

function blank(): BookingForm {
  return {
    id: '',
    ds: businessToday.value,
    y: 1,
    client: '',
    package: '',
    packageId: null,
    entryType: 'Package preset',
    destination: '',
    grossRevenue: 0,
    paymentStatus: 'Pending',
    bookingStatus: 'Pending',
    staffAssigned: 'Staff User',
    source: 'Package preset',
    notes: '',
    revision: null,
    mode: 'package',
  };
}

function bookingToForm(b: Booking): BookingForm {
  return {
    id: b.id,
    ds: b.ds,
    y: b.y,
    client: b.client,
    package: b.package,
    packageId: b.packageId,
    entryType: b.entryType,
    destination: b.destination,
    grossRevenue: b.grossRevenue,
    paymentStatus: b.paymentStatus,
    bookingStatus: b.bookingStatus,
    staffAssigned: b.staffAssigned,
    source: b.source,
    notes: b.notes ?? '',
    revision: b.revision,
    mode: b.packageId == null ? 'custom' : 'package',
  };
}

/** The first package with a seat free, so a new booking does not start on a full one. */
function firstOpenPackage(): PackageOption | undefined {
  return packages.value.find((p) => p.availableSlots > 0) ?? packages.value[0];
}

function applyPackage() {
  if (form.mode !== 'package') return;
  const preset = packages.value.find((p) => p.id === form.packageId);
  if (!preset) return;
  form.package = preset.packageName;
  form.destination = preset.destination;
  form.grossRevenue = Number(form.y || 1) * preset.basePrice;
  form.entryType = 'Package preset';
  form.source = 'Package preset';
}

function onPackageChange() {
  applyPackage();
  touch('packageId');
  touch('y');
}

function setMode(next: BookingMode) {
  if (form.mode === next) return;
  form.mode = next;
  if (next === 'custom') {
    form.packageId = null;
    form.entryType = 'Custom quotation';
    form.source = 'Manual quotation';
  } else {
    form.entryType = 'Package preset';
    form.source = 'Package preset';
    if (!form.packageId) form.packageId = firstOpenPackage()?.id ?? null;
    applyPackage();
  }
}

function clearFormState() {
  formError.value = '';
  mergeNotice.value = '';
  theirValues.value = {};
  confirmUnusual.value = false;
  serverUnusual.value = '';
  pendingRecovery.value = null;
  resetFieldErrors();
}

function openForm() {
  if (!businessToday.value) {
    formError.value = businessDateError;
    toast.error(businessDateError);
    return;
  }
  Object.assign(form, blank());
  form.id = nextCode('BKG', bookings.value.map((b) => b.id), 2401);
  form.packageId = firstOpenPackage()?.id ?? null;
  applyPackage();
  editing.value = false;
  originalCode.value = '';
  original.value = null;
  baseForm.value = null;
  drawerTitle.value = 'New booking';
  clearFormState();
  returnToDetail.value = false;
  detailOpen.value = false;
  openedSnapshot.value = snapshotForm();
  showForm.value = true;
}

function closeForm() {
  showForm.value = false;
  if (returnToDetail.value && detailBooking.value) detailOpen.value = true;
  returnToDetail.value = false;
}

function openEdit(b: Booking) {
  Object.assign(form, bookingToForm(b));
  original.value = b;
  baseForm.value = bookingToForm(b);
  editing.value = true;
  originalCode.value = b.id;
  drawerTitle.value = 'Edit booking';
  clearFormState();
  openedSnapshot.value = snapshotForm();
  showForm.value = true;
}

function describeValue(key: BookingField, value: unknown): string {
  switch (key) {
    case 'ds':
      return value ? formatDate(String(value)) : 'blank';
    case 'y':
      return count(Number(value));
    case 'grossRevenue':
      return peso(Number(value));
    case 'packageId':
      return packages.value.find((p) => p.id === value)?.packageName ?? (value ? String(value) : 'no package');
    case 'paymentStatus':
    case 'bookingStatus':
      return stateLabel(String(value ?? ''));
    default:
      return value ? `“${String(value)}”` : 'blank';
  }
}

function theirNote(key: BookingField): string {
  if (!(key in theirValues.value)) return '';
  return `Saved meanwhile as ${describeValue(key, theirValues.value[key])}.`;
}

function useTheirs(key: BookingField) {
  if (!(key in theirValues.value)) return;
  (form as Record<string, unknown>)[key] = theirValues.value[key];
  const rest = { ...theirValues.value };
  delete rest[key];
  theirValues.value = rest;
}

function describeMerge(changed: readonly (keyof BookingForm)[], conflicts: readonly (keyof BookingForm)[], voided: boolean): string {
  const named = (keys: readonly (keyof BookingForm)[]) =>
    keys.filter((key): key is BookingField => key in FIELD_LABELS).map((key) => FIELD_LABELS[key]);
  const taken = named(changed.filter((key) => !conflicts.includes(key)));
  const both = named(conflicts);
  const parts = ['Someone saved this booking while it was open here.'];
  if (voided) parts.push('It has also been voided.');
  if (taken.length) parts.push(`Their ${taken.length === 1 ? 'change' : 'changes'} to ${listNames(taken)} ${taken.length === 1 ? 'is' : 'are'} now in this form.`);
  if (both.length) parts.push(`You both changed ${listNames(both)}: your value is kept, with theirs shown beside it.`);
  parts.push('Your other changes are kept; save again when it looks right.');
  return parts.join(' ');
}

/**
 * After a conflict, the latest saved booking is merged into the form rather than replacing it:
 * nothing typed here is lost, and the next save goes through against the latest version.
 */
function applyMerge(): boolean {
  const saved = bookings.value.find((b) => b.id === originalCode.value);
  if (!saved || !baseForm.value) return false;
  const theirs = bookingToForm(saved);
  const mine: BookingForm = { ...form };
  const { merged, changedByOthers, conflicts } = mergeDraft(baseForm.value, mine, theirs, MERGE_KEYS);
  Object.assign(form, merged, { revision: saved.revision });
  const shown = conflicts.filter((key): key is BookingField & keyof BookingForm => key in FIELD_LABELS);
  theirValues.value = Object.fromEntries(shown.map((key) => [key, theirs[key]]));
  mergeNotice.value = describeMerge(changedByOthers, conflicts, !!saved.voided && !original.value?.voided);
  baseForm.value = theirs;
  original.value = saved;
  formError.value = '';
  recheck();
  return true;
}

async function load(showErrorToast = true): Promise<boolean> {
  try {
    const config = await api.appConfig();
    businessToday.value = config.today;
    const payload = await api.bookings();
    bookings.value = payload.bookings;
    packages.value = payload.packages;
    return true;
  } catch {
    if (showErrorToast) {
      if (!businessToday.value) toast.error(businessDateError);
      toast.error('We could not load the bookings. Please refresh and try again.');
    }
    return false;
  } finally {
    loading.value = false;
  }
}

function openImport() {
  importFile.value = null;
  importPreviewData.value = null;
  importError.value = '';
  showImport.value = true;
}

async function pickImportFile(event: Event) {
  const input = event.target as HTMLInputElement;
  const file = input.files?.[0] ?? null;
  input.value = '';
  if (!file) return;
  importFile.value = file;
  importPreviewData.value = null;
  importError.value = '';
  importBusy.value = true;
  try {
    importPreviewData.value = await api.importBookingsPreview(file);
  } catch (e) {
    importError.value = e instanceof ApiError ? Object.values(e.fields)[0] ?? e.message : 'Could not read that file.';
    importFile.value = null;
  } finally {
    importBusy.value = false;
  }
}

async function commitImport() {
  const file = importFile.value;
  if (!file || importBusy.value) return;
  importBusy.value = true;
  importError.value = '';
  try {
    const result = await api.importBookingsCommit(file);
    invalidateForecast();
    await load();
    showImport.value = false;
    toast.success(result.imported + ' booking' + (result.imported === 1 ? '' : 's') + ' imported');
  } catch (e) {
    importError.value = e instanceof ApiError ? Object.values(e.fields)[0] ?? e.message : 'Could not import the file.';
  } finally {
    importBusy.value = false;
  }
}

function conflictMessage(error: ApiError, noun: string): string {
  if (error.status !== 409) return Object.values(error.fields)[0] ?? error.message;
  if (error.code === 'duplicate_code') return error.message;
  if (error.code === 'stale_revision' || error.code === 'write_conflict') {
    return `This ${noun} changed while you were working. Review the refreshed record before saving again.`;
  }
  return Object.values(error.fields)[0] ?? error.message;
}

function sameBooking(saved: Booking, submitted: BookingInput): boolean {
  return saved.id === submitted.id
    && saved.ds === submitted.ds
    && saved.y === submitted.y
    && saved.client === submitted.client
    && saved.package === submitted.package
    && saved.packageId === submitted.packageId
    && saved.entryType === submitted.entryType
    && saved.destination === submitted.destination
    && saved.grossRevenue === submitted.grossRevenue
    && saved.paymentStatus === submitted.paymentStatus
    && saved.bookingStatus === submitted.bookingStatus
    && (saved.staffAssigned ?? '') === (submitted.staffAssigned ?? '')
    && saved.source === submitted.source
    && (saved.notes ?? '') === (submitted.notes ?? '');
}

function applyCreatedBookingRecovery(payload: BookingInput): boolean {
  const saved = bookings.value.find((b) => b.id === payload.id);
  if (!saved) return false;

  confirmUnusual.value = false;
  serverUnusual.value = '';
  if (sameBooking(saved, payload)) {
    showForm.value = false;
    toast.success('Booking ' + saved.id + ' saved');
    return true;
  }

  Object.assign(form, bookingToForm(saved));
  original.value = saved;
  baseForm.value = bookingToForm(saved);
  editing.value = true;
  originalCode.value = saved.id;
  drawerTitle.value = 'Edit booking';
  openedSnapshot.value = snapshotForm();
  formError.value = 'A booking with this ID is already saved with different details. Review the saved booking before changing it.';
  return true;
}

async function recoverCreatedBooking(payload: BookingInput): Promise<boolean> {
  if (!await load(false)) {
    pendingRecovery.value = { kind: 'create', payload };
    formError.value = createRecoveryError;
    return true;
  }
  return applyCreatedBookingRecovery(payload);
}

async function mergeAfterConflict(): Promise<boolean> {
  if (!await load(false)) {
    pendingRecovery.value = { kind: 'stale-edit' };
    formError.value = staleRecoveryError;
    return true;
  }
  if (applyMerge()) return true;
  formError.value = 'This booking was not found after reloading. Refresh the page before making another change.';
  return true;
}

async function continuePendingRecovery(): Promise<boolean> {
  const pending = pendingRecovery.value;
  if (!pending) return false;

  if (!await load(false)) {
    formError.value = pending.kind === 'create' ? createRecoveryError : staleRecoveryError;
    return true;
  }

  pendingRecovery.value = null;
  if (pending.kind === 'create') {
    if (applyCreatedBookingRecovery(pending.payload)) return true;
    formError.value = 'The booking was not found after reloading. Review the draft, then save again if it still needs to be created.';
    return true;
  }

  if (applyMerge()) return true;
  formError.value = 'The booking was not found after reloading. Refresh the page before making another change.';
  return true;
}

/** Where the server's field names land on this form; the trip fields follow the booking type. */
function serverFieldMap(): Record<string, BookingField> {
  const tripFields: Record<string, BookingField> = form.mode === 'package'
    ? { package: 'packageId', destination: 'packageId' }
    : { package: 'package', destination: 'destination' };
  return { ds: 'ds', y: 'y', client: 'client', grossRevenue: 'grossRevenue', packageId: 'packageId', ...tripFields };
}

function toPayload(): BookingInput {
  return {
    id: form.id,
    ds: form.ds,
    y: form.y,
    client: form.client,
    package: form.package,
    packageId: form.packageId,
    entryType: form.entryType,
    destination: form.destination,
    grossRevenue: form.grossRevenue,
    paymentStatus: form.paymentStatus,
    bookingStatus: form.bookingStatus,
    staffAssigned: form.staffAssigned,
    source: form.source,
    notes: form.notes,
    revision: form.revision,
    confirmUnusual: confirmUnusual.value,
  };
}

async function save() {
  if (saving.value) return;
  if (pendingRecovery.value) {
    formError.value = '';
    saving.value = true;
    try {
      await continuePendingRecovery();
    } finally {
      saving.value = false;
    }
    return;
  }

  formError.value = '';
  const found = validateAll();
  if (found.length > 0) {
    void focusField(found[0].key);
    return;
  }
  // Far above the usual price: ask once, beside the price, before anything is sent.
  if (unusualAmount.value && !confirmUnusual.value) {
    confirmUnusual.value = true;
    void focusField('grossRevenue');
    return;
  }

  saving.value = true;
  const payload = toPayload();
  const before = editing.value && original.value ? { ...original.value } : null;
  try {
    const saved = editing.value
      ? await api.updateBooking(originalCode.value, payload)
      : await api.createBooking(payload);
    invalidateForecast();
    await load();
    pendingRecovery.value = null;
    confirmUnusual.value = false;
    serverUnusual.value = '';
    historyVersion.value += 1;
    if (before) {
      toast.success('Booking ' + saved.id + ' saved', {
        action: undoAction(() => runUndo(`Booking ${saved.id}`, () => api.updateBooking(saved.id, bookingInput(before, saved.revision)), `Booking ${saved.id} is back as it was`)),
      });
    } else {
      toast.success('Booking ' + saved.id + ' saved', {
        action: { label: 'Open', run: () => openDetail(saved) },
      });
    }
    // An edit goes back to the booking it came from, now showing the change.
    if (editing.value) detailCode.value = payload.id;
    showForm.value = false;
    if (returnToDetail.value) detailOpen.value = true;
    returnToDetail.value = false;
  } catch (e) {
    if (e instanceof ApiError && e.status === 409 && e.code === 'unusual_revenue') {
      serverUnusual.value = e.message;
      confirmUnusual.value = true;
      void focusField('grossRevenue');
      return;
    }
    if (!editing.value && (!(e instanceof ApiError) || e.status >= 500 || e.status === 409)) {
      if (await recoverCreatedBooking(payload)) return;
    }
    if (e instanceof ApiError && e.status === 409) {
      if (editing.value && (e.code === 'stale_revision' || e.code === 'write_conflict') && await mergeAfterConflict()) return;
      await load();
    }
    if (e instanceof ApiError) {
      // A problem with one field goes beside that field; anything else is said at the top.
      const unplaced = placeServerErrors(e.fields, serverFieldMap());
      if (problems.value.length > 0) void focusField(problems.value[0].key);
      formError.value = unplaced[0] ?? (problems.value.length > 0 ? '' : conflictMessage(e, 'booking'));
    } else {
      formError.value = 'Could not reach the server, so nothing was saved. Your entries are still here; check the connection and save again.';
    }
  } finally {
    saving.value = false;
  }
}

const loadError = ref(false);

/** The first load failed: say so, rather than showing an empty list as if there were no records. */
async function retryLoad() {
  loadError.value = false;
  loading.value = true;
  if (!(await load())) loadError.value = true;
}

/** A link such as /bookings?open=BKG-2401 (from search or another page) opens that booking. */
function openFromAddress() {
  const code = typeof route.query.open === 'string' ? route.query.open : '';
  if (!code) return;
  const found = bookings.value.find((b) => b.id.toLowerCase() === code.toLowerCase());
  if (found) openDetail(found);
  void router.replace({ query: { ...route.query, open: undefined } });
}

onMounted(async () => {
  if (!(await load())) {
    loadError.value = true;
    return;
  }
  openFromAddress();
  // "Record a booking" arrives with ?new=1, and a package's "Book this package" adds ?package=.
  if (route.query.new === '1') {
    openForm();
    const chosen = typeof route.query.package === 'string' ? route.query.package : '';
    if (chosen && packages.value.some((p) => p.id === chosen)) {
      form.packageId = chosen;
      applyPackage();
      openedSnapshot.value = snapshotForm();
    }
    void router.replace({ query: { ...route.query, new: undefined, package: undefined } });
  }
});

watch(() => route.query.open, (value) => {
  if (value && !loading.value) openFromAddress();
});
</script>

<style scoped>
.booking-mode {
  border: none;
  padding: 0;
  margin: 0 0 var(--space-5);
  min-width: 0;
}

.booking-mode legend {
  padding: 0;
  margin-bottom: 8px;
}

.booking-mode-options {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 4px;
  padding: 4px;
  background: var(--color-surface-sunken);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
}

.booking-mode-option {
  position: relative;
  display: flex;
  align-items: center;
  justify-content: center;
  text-align: center;
  padding: 8px 10px;
  border-radius: var(--radius-sm);
  color: var(--color-text-muted);
  font-size: var(--text-sm);
  font-weight: 500;
  cursor: pointer;
  transition: background var(--transition-fast), color var(--transition-fast);
}

.booking-mode-option:hover {
  color: var(--color-text-primary);
}

.booking-mode-option input {
  position: absolute;
  width: 1px;
  height: 1px;
  opacity: 0;
  margin: 0;
}

.booking-mode-option.selected {
  background: var(--color-surface);
  color: var(--color-primary);
  box-shadow: var(--shadow-md);
}

.booking-mode-option:focus-within {
  outline: 2px solid var(--color-ring);
  outline-offset: 1px;
}

.trip-name {
  display: block;
  color: var(--color-text-primary);
}

.kind-tag {
  display: inline-block;
  margin-left: 6px;
  padding: 0 7px;
  border-radius: var(--radius-pill);
  background: var(--tone-primary-surface);
  color: var(--tone-primary-ink);
  font-size: var(--text-xs);
  font-weight: 600;
}
</style>
