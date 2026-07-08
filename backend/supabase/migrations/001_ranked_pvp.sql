-- Dream Gate ranked PvP: profiles, matchmaking, history, achievements

create extension if not exists "pgcrypto";

create table if not exists public.player_profiles (
    id uuid primary key references auth.users (id) on delete cascade,
    display_name text not null default 'Dreamer',
    mmr integer not null default 1500 check (mmr >= 0),
    highest_mmr integer not null default 1500 check (highest_mmr >= 0),
    rated_games_played integer not null default 0 check (rated_games_played >= 0),
    wins integer not null default 0 check (wins >= 0),
    losses integer not null default 0 check (losses >= 0),
    top4_finishes integer not null default 0 check (top4_finishes >= 0),
    current_win_streak integer not null default 0 check (current_win_streak >= 0),
    best_win_streak integer not null default 0 check (best_win_streak >= 0),
    total_damage_dealt bigint not null default 0 check (total_damage_dealt >= 0),
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

create table if not exists public.match_queue (
    id uuid primary key default gen_random_uuid(),
    player_id uuid not null references public.player_profiles (id) on delete cascade,
    mmr integer not null,
    display_name text not null,
    queued_at timestamptz not null default now(),
    status text not null default 'searching' check (status in ('searching', 'matched', 'cancelled')),
    unique (player_id)
);

create table if not exists public.matches (
    id uuid primary key default gen_random_uuid(),
    lobby_id text not null unique,
    match_seed integer not null,
    used_bot_fill boolean not null default false,
    human_count integer not null check (human_count between 1 and 8),
    status text not null default 'pending' check (status in ('pending', 'active', 'completed', 'cancelled')),
    match_server_url text,
    created_at timestamptz not null default now(),
    started_at timestamptz,
    ended_at timestamptz
);

create table if not exists public.match_slots (
    match_id uuid not null references public.matches (id) on delete cascade,
    slot_index integer not null check (slot_index between 0 and 7),
    is_bot boolean not null,
    player_id uuid references public.player_profiles (id) on delete set null,
    display_name text not null,
    primary key (match_id, slot_index)
);

create table if not exists public.match_history (
    id uuid primary key default gen_random_uuid(),
    match_id uuid not null references public.matches (id) on delete cascade,
    lobby_id text not null,
    match_seed integer not null,
    used_bot_fill boolean not null,
    human_count integer not null,
    started_at timestamptz not null default now(),
    ended_at timestamptz
);

create table if not exists public.match_participants (
    match_history_id uuid not null references public.match_history (id) on delete cascade,
    slot_index integer not null check (slot_index between 0 and 7),
    player_id uuid references public.player_profiles (id) on delete set null,
    is_bot boolean not null,
    display_name text not null,
    placement integer not null check (placement between 1 and 8),
    mmr_before integer,
    mmr_after integer,
    mmr_delta integer,
    damage_dealt integer not null default 0,
    hero_name text,
    primary key (match_history_id, slot_index)
);

create table if not exists public.achievements (
    id text primary key,
    name text not null,
    description text not null
);

create table if not exists public.player_achievements (
    player_id uuid not null references public.player_profiles (id) on delete cascade,
    achievement_id text not null references public.achievements (id) on delete cascade,
    unlocked_at timestamptz not null default now(),
    primary key (player_id, achievement_id)
);

insert into public.achievements (id, name, description) values
    ('first_rated_game', 'First Steps', 'Play your first rated match.'),
    ('first_win', 'Victory', 'Win your first rated match.'),
    ('win_streak_3', 'On Fire', 'Win 3 rated matches in a row.'),
    ('win_streak_5', 'Unstoppable', 'Win 5 rated matches in a row.'),
    ('top4_streak_5', 'Consistent', 'Finish top 4 in five rated matches in a row.'),
    ('giant_slayer', 'Giant Slayer', 'Beat a player 300+ MMR higher than you.')
on conflict (id) do nothing;

create index if not exists idx_match_queue_status_queued_at on public.match_queue (status, queued_at);
create index if not exists idx_match_queue_mmr on public.match_queue (mmr) where status = 'searching';
create index if not exists idx_matches_status on public.matches (status);

alter table public.player_profiles enable row level security;
alter table public.match_queue enable row level security;
alter table public.matches enable row level security;
alter table public.match_slots enable row level security;
alter table public.match_history enable row level security;
alter table public.match_participants enable row level security;
alter table public.achievements enable row level security;
alter table public.player_achievements enable row level security;

create policy "profiles_select_own" on public.player_profiles
    for select using (auth.uid() = id);

create policy "profiles_insert_own" on public.player_profiles
    for insert with check (auth.uid() = id);

create policy "profiles_update_own" on public.player_profiles
    for update using (auth.uid() = id);

create policy "queue_select_own" on public.match_queue
    for select using (auth.uid() = player_id);

create policy "queue_insert_own" on public.match_queue
    for insert with check (auth.uid() = player_id);

create policy "queue_update_own" on public.match_queue
    for update using (auth.uid() = player_id);

create policy "queue_delete_own" on public.match_queue
    for delete using (auth.uid() = player_id);

create policy "matches_select_participant" on public.matches
    for select using (
        exists (
            select 1 from public.match_slots ms
            where ms.match_id = matches.id and ms.player_id = auth.uid()
        )
    );

create policy "match_slots_select_participant" on public.match_slots
    for select using (
        exists (
            select 1 from public.match_slots ms
            where ms.match_id = match_slots.match_id and ms.player_id = auth.uid()
        )
    );

create policy "achievements_read_all" on public.achievements
    for select using (true);

create policy "player_achievements_select_own" on public.player_achievements
    for select using (auth.uid() = player_id);

create or replace function public.touch_player_profile_updated_at()
returns trigger
language plpgsql
as $$
begin
    new.updated_at = now();
    return new;
end;
$$;

drop trigger if exists trg_player_profiles_updated_at on public.player_profiles;
create trigger trg_player_profiles_updated_at
    before update on public.player_profiles
    for each row execute function public.touch_player_profile_updated_at();

create or replace function public.handle_new_user()
returns trigger
language plpgsql
security definer
set search_path = public
as $$
begin
    insert into public.player_profiles (id, display_name)
    values (
        new.id,
        coalesce(new.raw_user_meta_data ->> 'display_name', split_part(new.email, '@', 1), 'Dreamer')
    )
    on conflict (id) do nothing;
    return new;
end;
$$;

drop trigger if exists on_auth_user_created on auth.users;
create trigger on_auth_user_created
    after insert on auth.users
    for each row execute function public.handle_new_user();