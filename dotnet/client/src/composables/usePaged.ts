import { computed, ref, watch, type Ref } from 'vue';

export function usePaged<T>(source: Ref<readonly T[]>, size = 25) {
  const limit = ref(size);

  watch(
    () => source.value.length,
    () => {
      limit.value = size;
    },
  );

  return {
    visible: computed(() => source.value.slice(0, limit.value)),
    total: computed(() => source.value.length),
    shown: computed(() => Math.min(limit.value, source.value.length)),
    loadMore: () => {
      limit.value += size;
    },
    showAll: () => {
      limit.value = source.value.length;
    },
  };
}
