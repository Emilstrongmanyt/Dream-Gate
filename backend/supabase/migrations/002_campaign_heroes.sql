-- Campaign progress and hero portrait collection

alter table public.player_profiles
    add column if not exists selected_hero_id text not null default 'hero_art_Warrior',
    add column if not exists unlocked_hero_ids_csv text not null default 'hero_art_Warrior',
    add column if not exists campaign_highest_level integer not null default 0 check (campaign_highest_level >= 0);

create table if not exists public.player_campaign_completions (
    player_id uuid not null references public.player_profiles (id) on delete cascade,
    mission_level integer not null check (mission_level > 0),
    unlocked_hero_id text not null,
    completed_at timestamptz not null default now(),
    primary key (player_id, mission_level)
);

alter table public.player_campaign_completions enable row level security;

create policy "campaign_completions_select_own" on public.player_campaign_completions
    for select using (auth.uid() = player_id);

create policy "campaign_completions_insert_own" on public.player_campaign_completions
    for insert with check (auth.uid() = player_id);

create policy "campaign_completions_update_own" on public.player_campaign_completions
    for update using (auth.uid() = player_id);