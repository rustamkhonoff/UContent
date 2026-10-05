# UContent

UContent — сервис управления контентом для Unity поверх Addressables и UniTask.
Он загружает assets, создаёт GameObject, управляет сценами и скачивает зависимости.
Главное дополнение к Addressables — явное владение ресурсами и объединение запросов.

## Зависимости

- Unity 2021.3 или новее согласно package.json.
- UniTask 2.5.0 или новее; установите пакет через Git URL или настроенный registry Cysharp.
- Addressables 1.21.21 или новее.
- VContainer — необязательная интеграция; включается по пакету `jp.hadashikick.vcontainer`.

Все обращения к сервису, handles, scopes и scenes выполняйте в главном потоке Unity.
Отмену токенов, используемых в этих операциях, также выполняйте в главном потоке.
Сервис и scope не предназначены для параллельного доступа с фоновых потоков.

## 1. Сервис и handle

```csharp
IContentService content = new AddressablesContentService();
await content.InitializeAsync();

using (var handle = await content.LoadAsync<Sprite>("icons/coin"))
{
    image.sprite = handle.Value;
    await WaitUntilTheWindowClosesAsync(); // ваш метод ожидания
    image.sprite = null;
}
```

`ContentHandle<T>` — владелец ресурса. Пока handle жив, он удерживает загрузку.
`Dispose()` отдаёт владение обратно сервису; повторный вызов безопасен.
После освобождения `Value` бросает `ObjectDisposedException`, а `TryGetValue` возвращает false.

Ссылка на `Sprite` или `GameObject` сама по себе не удерживает Addressables handle.
Не освобождайте handle, пока ресурс нужен окну, объектам или другим потребителям.
Не вызывайте `Addressables.Release` для ресурса, которым владеет UContent.

## 2. Почему загрузки объединяются

Два `LoadAsync<Sprite>("icons/coin")` на одном экземпляре сервиса используют одну
внутреннюю `SharedContentEntry<Sprite>`, но возвращают два отдельных handles.

```text
окно A → handle A ┐
                 ├→ SharedContentEntry<Sprite> → Addressables handle
окно B → handle B ┘
```

Освобождение handle A оставляет ресурс для B. После освобождения последнего владельца
сервис освобождает Addressables handle и удаляет запись. Это не постоянный кеш:
ресурс сохраняется, пока есть владельцы или пока ещё завершается сама операция.

Ключ объединения — пара `(тип asset, ключ)`. Поэтому другой тип или другой экземпляр
сервиса создаёт отдельную запись. Строковые адреса удобны как стабильные ключи.

Владелец добавляется до запуска операции: Addressables может вернуть готовый результат
сразу, без перехода на следующий кадр. Это важно и для повторных загрузок, и для ошибок.

`LoadAllAsync` возвращает один handle на всю коллекцию. На уровне UContent такие
загрузки не объединяются с одиночными и друг с другом; Addressables управляет своими
внутренними зависимостями отдельно.

## 3. Scope для окна или уровня

```csharp
var scope = content.CreateScope("InventoryWindow");
try
{
    var icon = await scope.LoadAsync<Sprite>("icons/coin");
    var item = await scope.InstantiateAsync("ui/item", container);
    await WaitUntilTheWindowClosesAsync(); // ваш метод ожидания
}
finally
{
    scope.Dispose();
}
```

Scope хранит полученные handles и instances, поэтому возвращает непосредственно asset
или GameObject. Он освобождает всё в обратном порядке. Это удобно, когда ресурсы имеют
одинаковую продолжительность жизни: окно, игровой режим, уровень.

`Clear()` освобождает уже зарегистрированные ресурсы, но оставляет scope пригодным
для новых загрузок. Он не отменяет ожидающие загрузки: они могут зарегистрироваться
после Clear. `Dispose()` закрывает scope окончательно; если ожидающая загрузка закончится
после закрытия, её handle будет освобождён, а вызов получит `ObjectDisposedException`.

Сцены не входят в scope: их освобождение асинхронное, а Dispose у scope синхронный.
Сценой управляйте через `ContentScene` и `await UnloadAsync()`.

## 4. Asset и instance — разные вещи

`LoadAsync<GameObject>` загружает prefab, но не создаёт объект в сцене.
`InstantiateAsync` создаёт объект и возвращает `ContentInstance`.

```csharp
var instance = await content.InstantiateAsync("characters/hero", parent);
// instance.Instance — GameObject; instance.Transform — его Transform.
instance.Dispose();
```

`Dispose()` у instance вызывает освобождение экземпляра через Addressables.
Не сочетайте этот путь с ручным `Destroy` и `Addressables.ReleaseInstance` для того
же объекта. Для объектов, созданных обычным `Object.Instantiate(handle.Value)`, сами
уничтожьте все копии до освобождения исходного prefab handle.

## 5. Отмена ожидания

Общая загрузка может понадобиться нескольким клиентам. Отмена токена одного клиента
прекращает его ожидание и снимает его владение, сохраняя остальных клиентов.
Если отменились все, операция Addressables всё равно может продолжаться; после
завершения её результат освобождается, если владельцев уже нет.

Отмена `DownloadAsync` тоже прекращает ожидание конкретного клиента. Она не обещает
остановить сеть или удалить уже скачанные данные. Токен, отменённый до вызова
`LoadAsync` или `DownloadAsync`, не запускает новую операцию.

`UpdateCatalogsAsync` проверяет отмену до запуска, затем дожидается обновления:
начавшееся изменение каталога не прерывается токеном. У сцен токена отмены нет.

## 6. Скачивание и загрузка — отдельные шаги

```csharp
var bytes = await content.GetDownloadSizeAsync("level-1");
await content.DownloadAsync("level-1", progress);
var scene = await content.LoadSceneAsync("level-1", LoadSceneMode.Additive);
```

Download скачивает зависимости в дисковый кеш. Load загружает ресурс для использования
в игре. Успешное скачивание не создаёт GameObject и не активирует сцену.

Одинаковые Download-запросы объединяются, пока операция выполняется. Для набора ключей
порядок ключей и MergeMode участвуют в сравнении. `Union` объединяет найденные ресурсы,
`Intersection` оставляет общие, `UseFirst` использует первый ключ с найденными ресурсами.

Прогресс сообщает байты и признак завершения. Ошибка непосредственного вызова
`IProgress.Report` логируется и не ломает скачивание для остальных клиентов.
Если используете `System.Progress<T>` с отложенным вызовом обработчика, исключение
внутри этого обработчика нужно обработать там: оно возникает уже вне Report.

## 7. Сцены

```csharp
var scene = await content.LoadSceneAsync(
    "level-1", LoadSceneMode.Additive, activateOnLoad: false);

await scene.ActivateAsync();
// Уровень используется игрой.
await scene.UnloadAsync();
```

Неактивированная сцена блокирует очередь асинхронных операций Unity. Поэтому между
загрузкой с `activateOnLoad: false` и активацией не ожидайте другие операции, которым
нужна эта очередь.

Повторные вызовы ActivateAsync и UnloadAsync во время выполнения ожидают одну общую
операцию. IsUnloaded становится true только после успешной выгрузки. После ошибки
активации или выгрузки разрешён повторный вызов. Выгрузка неактивированной сцены сначала
активирует её, чтобы разблокировать очередь; при этом код сцены может начать выполняться.

## 8. DI и диагностика

```csharp
builder.RegisterUContent();
builder.RegisterContentScope("Gameplay");
```

VContainer регистрирует сервис как Singleton, чтобы потребители одного контейнера
использовали общие загрузки. ContentScope имеет Lifetime.Scoped и освобождается вместе
с соответствующим DI scope.

Окно `Tools → UContent → Diagnostics` показывает живые wrappers: handles, instances,
scopes и scenes. Enabled влияет на новые регистрации. Capture Stack Trace помогает
понять, где создан объект, который забыли освободить. Это не измеритель памяти Unity:
число записей отражает объекты владения UContent, а не число assets или bundles.

## 9. Навигация по исходникам

- `Contracts/IContentService.cs`: публичные операции.
- `Implementation/AddressablesContentService.cs`: вызовы Addressables и владение handles.
- `Internal/SharedContentEntry.cs`: одна загрузка, много владельцев.
- `Internal/SharedDownloadOperation.cs`: общее скачивание и подписчики прогресса.
- `Types/ContentHandle.cs`, `ContentInstance.cs`: освобождение отдельного ресурса.
- `Types/ContentScope.cs`: владение группой ресурсов.
- `Types/ContentScene.cs`: асинхронная активация и выгрузка.
- `Diagnostics`: записи для поиска забытых владельцев.

Проверки жизненного цикла находятся в `Tests~/RegressionHarness`.
