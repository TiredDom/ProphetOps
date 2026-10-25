import { computed, nextTick, reactive, ref } from 'vue';

export type FieldRules<K extends string> = Partial<Record<K, () => string>>;

export interface FieldProblem<K extends string> {
  key: K;
  label: string;
  message: string;
}

/**
 * Field-by-field checks for a drawer form. A field's message appears beside it once someone
 * has left the field or tried to save, and goes the moment the value is put right; nothing
 * waits for the server to say what a glance at the form could.
 *
 * `labels` lists the fields in the order they appear, which is the order problems are named
 * and the first one is focused.
 */
export function useFieldErrors<K extends string>(
  idPrefix: string,
  labels: Record<K, string>,
  rules: FieldRules<K>,
) {
  const errors = reactive({}) as Partial<Record<K, string>>;
  const touched = new Set<K>();
  // Set by a save attempt, so the list of problems shows only after someone has tried.
  const attempted = ref(false);
  const order = Object.keys(labels) as K[];

  function run(key: K): string {
    const rule = rules[key];
    return rule ? rule() : '';
  }

  function check(key: K) {
    const message = run(key);
    if (message) errors[key] = message;
    else delete errors[key];
  }

  /** A field was left: say now what is wrong with it. */
  function touch(key: K) {
    touched.add(key);
    check(key);
  }

  /** Checks again the fields already left, so a fixed value loses its message as it is typed. */
  function recheck() {
    for (const key of touched) check(key);
  }

  /** Checks every field; returns the problems in the order they appear on the form. */
  function validateAll(): FieldProblem<K>[] {
    attempted.value = true;
    for (const key of order) {
      touched.add(key);
      check(key);
    }
    return problems.value;
  }

  /** A message from the server about one field, shown beside it like any other. */
  function setError(key: K, message: string) {
    touched.add(key);
    errors[key] = message;
  }

  /**
   * Puts the server's field messages beside their fields. Returns what could not be placed,
   * for the notice at the top of the form.
   */
  function placeServerErrors(fields: Record<string, string>, map: Partial<Record<string, K>>): string[] {
    const unplaced: string[] = [];
    for (const [name, message] of Object.entries(fields)) {
      const key = map[name] ?? (name in labels ? (name as K) : undefined);
      if (key) setError(key, message);
      else unplaced.push(message);
    }
    if (Object.keys(fields).length > 0) attempted.value = true;
    return unplaced;
  }

  function reset() {
    for (const key of Object.keys(errors) as K[]) delete errors[key];
    touched.clear();
    attempted.value = false;
  }

  const problems = computed<FieldProblem<K>[]>(() =>
    order
      .filter((key) => errors[key])
      .map((key) => ({ key, label: labels[key], message: errors[key] as string })),
  );

  function fieldId(key: K): string {
    return `${idPrefix}-${key}`;
  }

  /** Moves focus to a field and brings it into view, for the list of problems and after a save. */
  async function focusField(key: K) {
    await nextTick();
    const el = document.getElementById(fieldId(key));
    if (!el) return;
    el.focus({ preventScroll: true });
    const reduce = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    el.scrollIntoView({ block: 'center', behavior: reduce ? 'auto' : 'smooth' });
  }

  return {
    errors,
    attempted,
    problems,
    touch,
    recheck,
    validateAll,
    setError,
    placeServerErrors,
    reset,
    fieldId,
    focusField,
  };
}
