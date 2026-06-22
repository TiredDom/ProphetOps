<?php

namespace App\Http\Controllers;

use App\Models\Expense;
use App\Support\ProphetOpsData;
use Illuminate\Http\RedirectResponse;
use Illuminate\Http\Request;
use Illuminate\Validation\Rule;
use Inertia\Inertia;
use Inertia\Response;

class ExpenseController extends Controller
{
    public function index(): Response
    {
        return Inertia::render('Expenses', [
            'expenses' => ProphetOpsData::expenses(),
        ]);
    }

    public function store(Request $request): RedirectResponse
    {
        $data = $this->validated($request);
        $expense = Expense::query()->create($this->payload($data));

        return back()->with('notice', [
            'tone' => 'success',
            'title' => 'Expense added',
            'message' => $expense->code.' was saved to the database.',
        ]);
    }

    public function update(Request $request, Expense $expense): RedirectResponse
    {
        $data = $this->validated($request, $expense);
        $expense->update($this->payload($data));

        return back()->with('notice', [
            'tone' => 'success',
            'title' => 'Expense updated',
            'message' => $expense->code.' was saved to the database.',
        ]);
    }

    public function bulk(Request $request): RedirectResponse
    {
        $data = $request->validate([
            'ids' => ['required', 'array', 'min:1'],
            'ids.*' => ['string', 'exists:expenses,code'],
            'action' => ['required', Rule::in(['paid', 'pending'])],
        ]);
        $status = $data['action'] === 'paid' ? 'Paid' : 'Pending';

        Expense::query()->whereIn('code', $data['ids'])->update(['payment_status' => $status]);

        return back()->with('notice', [
            'tone' => $status === 'Paid' ? 'success' : 'warning',
            'title' => count($data['ids']).' expense'.(count($data['ids']) === 1 ? '' : 's').' updated',
            'message' => 'Payment status changed to '.$status.'.',
        ]);
    }

    private function validated(Request $request, ?Expense $expense = null): array
    {
        return $request->validate([
            'id' => ['required', 'string', 'max:40', Rule::unique('expenses', 'code')->ignore($expense?->id)],
            'date' => ['required', 'date'],
            'category' => ['required', Rule::in(['Tour operations', 'Marketing', 'Seasonal cost', 'Overhead'])],
            'amount' => ['required', 'integer', 'min:0'],
            'package' => ['required', 'string', 'max:120'],
            'paymentStatus' => ['required', Rule::in(['Paid', 'Pending'])],
            'notes' => ['nullable', 'string', 'max:320'],
        ]);
    }

    private function payload(array $data): array
    {
        return [
            'code' => $data['id'],
            'expense_date' => $data['date'],
            'category' => $data['category'],
            'amount' => $data['amount'],
            'related_package' => $data['package'],
            'payment_status' => $data['paymentStatus'],
            'notes' => $data['notes'] ?? null,
        ];
    }
}
